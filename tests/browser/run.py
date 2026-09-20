"""Isolated full-stack browser test runner; never uses configured app secrets."""
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
from urllib.request import urlopen

root = Path(__file__).resolve().parents[2]
client = Path(os.environ.get('SONGVOTER_CLIENT', root.parent / 'song_voter'))
container = 'songvoter-browser-' + secrets.token_hex(5)
api_port = os.environ.get('API_PORT', '4208')
web_port = os.environ.get('WEB_PORT', '4308')
processes = []


def run(*args, **kwargs):
    return subprocess.run(args, check=True, **kwargs)


def wait(url):
    for _ in range(90):
        try:
            with urlopen(url, timeout=2) as response:
                if response.status == 200:
                    return
        except OSError:
            time.sleep(1)
    raise RuntimeError(f'Timed out waiting for {url}')


try:
    run('dotnet', 'build', '--no-incremental', cwd=root)
    if os.environ.get('SKIP_WEB_BUILD') != '1':
        run(os.environ.get('FLUTTER_BIN', 'flutter'), 'build', 'web', '--release', cwd=client)
        run('sh', str(client / 'tool/fingerprint-web.sh'), cwd=client)
    run('npm', 'ci', cwd=Path(__file__).parent)
    run('docker', 'run', '-d', '--name', container, '-p', '127.0.0.1::5432',
        '-e', 'POSTGRES_USER=songvoter', '-e', 'POSTGRES_PASSWORD=songvoter',
        '-e', 'POSTGRES_DB=songvoter', 'postgres:18-alpine', stdout=subprocess.DEVNULL)
    port = subprocess.check_output(['docker', 'port', container, '5432'], text=True).strip().split(':')[-1]
    for _ in range(60):
        if subprocess.run(['docker', 'exec', container, 'pg_isready', '-U', 'songvoter'],
                          stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
            break
        time.sleep(1)
    env = dict(os.environ, DB_CONNECTION=f'Host=127.0.0.1;Port={port};Database=songvoter;Username=songvoter;Password=songvoter',
               jwt__secret=secrets.token_hex(32), hashids__salt='browser-tests', Authentication__Difficulty='16',
               spotify__clientid='', spotify__clientsecret='', youtube__apiKey='',
               ASPNETCORE_URLS=f'http://127.0.0.1:{api_port}', OPENBAO__ENABLED='false', OPENBAO__ADDR='')
    dll = str(root / 'bin/Debug/net10.0/Coflnet.SongVoter.dll')
    with tempfile.TemporaryFile() as log:
        run('dotnet', dll, '--migrate-only', cwd=root, env=env, stdout=log, stderr=log)
        sql = '''INSERT INTO "Songs" ("Title","Lookup") VALUES ('Midnight City','midnightcitym83'),('Feel Good Inc.','feelgoodincgorillaz');
INSERT INTO "ExternalSongs" ("Platform","Title","Artist","ExternalId","Duration","PlayCounter","SongId") VALUES
(1,'Midnight City','M83','dX3k_QDnzHE',INTERVAL '4 minutes',0,1),(2,'Feel Good Inc.','Gorillaz','0d28khcov6AiegSCpG5TuT',INTERVAL '4 minutes',0,2);'''
        run('docker', 'exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'songvoter', input=sql, text=True, stdout=log)
        processes.append(subprocess.Popen(['dotnet', dll], cwd=root, env=env, stdout=log, stderr=log))
        try:
            wait(f'http://127.0.0.1:{api_port}/status')
            processes.append(subprocess.Popen(['python3', str(Path(__file__).with_name('serve.py'))],
                             env=dict(os.environ, API_PORT=api_port, WEB_PORT=web_port, WEB_ROOT=str(client / 'build/web')),
                             stdout=log, stderr=log))
            wait(f'http://127.0.0.1:{web_port}')
            run('npx', 'playwright', 'test', cwd=Path(__file__).parent,
                env=dict(os.environ, API_URL=f'http://127.0.0.1:{api_port}', WEB_URL=f'http://127.0.0.1:{web_port}'))
            if os.environ.get('ANDROID_DEVICE'):
                run(os.environ.get('FLUTTER_BIN', 'flutter'), 'test', 'integration_test/host_test.dart',
                    '-d', os.environ['ANDROID_DEVICE'], f'--dart-define=API_BASE_URL=http://10.0.2.2:{api_port}',
                    '--dart-define=LIVE_PLAYBACK=' + os.environ.get('LIVE_PLAYBACK', 'false'), cwd=client)
        except Exception:
            log.seek(0)
            print(log.read().decode(errors='replace')[-12000:])
            raise
finally:
    for process in reversed(processes):
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
    subprocess.run(['docker', 'rm', '-f', container], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

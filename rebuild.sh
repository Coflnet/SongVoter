docker run --rm \
    -u $(id -u ${USER}):$(id -g ${USER}) \
    -v $PWD:/local openapitools/openapi-generator-cli generate \
    -i /local/api.yaml \
    -g aspnetcore \
    --additional-properties=packageName= \
    -o /local/
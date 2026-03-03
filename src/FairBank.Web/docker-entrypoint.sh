#!/bin/sh
set -e

CONF_TEMPLATE="/etc/nginx/conf.d/default.conf.template"
CONF_OUTPUT="/etc/nginx/conf.d/default.conf"

# Default: caching enabled
ENABLE_CACHE="${ENABLE_CACHE:-true}"

if [ "$ENABLE_CACHE" = "true" ] || [ "$ENABLE_CACHE" = "1" ]; then
    CACHE_CONFIG='
    location ~* \.(dll|wasm|dat|blat)$ {
        expires 7d;
        add_header Cache-Control "public, immutable";
    }

    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff2?)$ {
        expires 1d;
        add_header Cache-Control "public, must-revalidate";
    }'
else
    CACHE_CONFIG='
    # Caching DISABLED (ENABLE_CACHE=false)
    add_header Cache-Control "no-store, no-cache, must-revalidate, max-age=0" always;
    add_header Pragma "no-cache" always;
    expires -1;'
fi

# Build the final nginx config from template
# Read template line by line and replace placeholder markers
{
    while IFS= read -r line; do
        case "$line" in
            *__CACHE_BLOCK_START__*)
                printf '%s\n' "$CACHE_CONFIG"
                # Skip lines until end marker
                while IFS= read -r skip; do
                    case "$skip" in
                        *__CACHE_BLOCK_END__*) break ;;
                    esac
                done
                ;;
            *)
                printf '%s\n' "$line"
                ;;
        esac
    done
} < "$CONF_TEMPLATE" > "$CONF_OUTPUT"

echo "Nginx cache config: ENABLE_CACHE=$ENABLE_CACHE"

# Start nginx
exec nginx -g 'daemon off;'

#!/usr/bin/env python3
"""Token chỉ đọc từ config root; không nằm trong argv, URL log hoặc release."""
import re
import sys
import urllib.parse
import urllib.request

def main():
    values = {}
    with open('/opt/junglemaze/config/.env.aws', encoding='utf-8') as config:
        for line in config:
            name, _, value = line.strip().partition('=')
            values[name] = value.strip("'")
    domain = values.get('PUBLIC_DOMAIN', '')
    token = values.get('DUCKDNS_TOKEN', '')
    if not re.fullmatch(r'[a-z0-9-]+\.duckdns\.org', domain) or not token:
        raise ValueError('Invalid config')
    # ip trống: DuckDNS nhận IPv4 egress hiện tại của EC2, không cần AWS access key.
    query = urllib.parse.urlencode(dict(domains=domain.removesuffix('.duckdns.org'), token=token, ip=''))
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with opener.open('https://www.duckdns.org/update?' + query, timeout=15) as response:
        if response.read(512).decode().strip() != 'OK':
            raise ValueError('DuckDNS rejected update')
    print('DuckDNS updated: ' + domain)

if __name__ == '__main__':
    try:
        main()
    except Exception:
        # URLError có thể chứa full URL/token: không in exception hoặc traceback.
        print('DuckDNS update failed; check token/network/config. Timer will retry.', file=sys.stderr)
        sys.exit(1)

"""Embed the repository's GitHub Releases link into the EXE at publish time.

Chrome Store URL can be set separately after approval using ExtensionLinks.json
next to the standalone EXE; it is optional and does not block the build.
"""
import argparse
import json
from pathlib import Path
from urllib.parse import urlparse

parser = argparse.ArgumentParser()
parser.add_argument('--github-releases-url', required=True)
args = parser.parse_args()
url = args.github_releases_url
parsed = urlparse(url)
if parsed.scheme != 'https' or parsed.netloc != 'github.com' or not parsed.path.endswith('/releases'):
    raise SystemExit('Expected https://github.com/OWNER/REPO/releases')
path = Path(__file__).resolve().parents[1] / 'PhotoSafeCheck' / 'ExtensionLinks.json'
config = json.loads(path.read_text(encoding='utf-8'))
config['githubReleasesUrl'] = url
path.write_text(json.dumps(config, indent=2) + '\n', encoding='utf-8')
print('Embedded GitHub Releases URL:', url)

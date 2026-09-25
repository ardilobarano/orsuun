"""Runs one fal.ai model through its queue and downloads what it returns.

    python3 tools/art/fal.py <endpoint> <args.json | inline json> <out-dir> <name>
    python3 tools/art/fal.py fetch <response_url> <out-dir> <name>      (a job submitted earlier)

Strings in the arguments that start with "@" are local files, sent as data URIs ("@crops/wolf-front.jpg"). Every URL in
the result (images, meshes) is saved into <out-dir> as <name>[-n].<ext>; the result JSON is printed without them.
The key is read from ~/.config/fal/key (never from the command line, never printed).
"""
import base64
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request

KEY = open(os.path.expanduser("~/.config/fal/key")).read().strip()


def request(url, body=None, tries=8):
    """One API call, retried with a growing pause on network errors (a dropped connection must not lose a paid job)."""
    data = json.dumps(body).encode() if body is not None else None
    for attempt in range(tries):
        req = urllib.request.Request(url, data=data, method="POST" if data else "GET",
                                     headers={"Authorization": "Key " + KEY, "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                return json.loads(r.read())
        except urllib.error.HTTPError as e:
            # fal now and then refuses a submission with 403 (or 429) and takes the same one a little later.
            if e.code not in (403, 429) or attempt == tries - 1:
                raise
            print("refused:", e.code, "- retrying", flush=True)
            time.sleep(min(60, 5 * 2 ** attempt))
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            if attempt == tries - 1:
                raise
            print("network:", e, "- retrying", flush=True)
            time.sleep(min(60, 5 * 2 ** attempt))


def download(url, path, tries=8):
    for attempt in range(tries):
        try:
            urllib.request.urlretrieve(url, path)
            return
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            if attempt == tries - 1:
                raise
            print("network:", e, "- retrying", flush=True)
            time.sleep(min(60, 5 * 2 ** attempt))


def inline_files(value):
    if isinstance(value, str) and value.startswith("@"):
        path = value[1:]
        mime = mimetypes.guess_type(path)[0] or "application/octet-stream"
        return "data:%s;base64,%s" % (mime, base64.b64encode(open(path, "rb").read()).decode())
    if isinstance(value, list):
        return [inline_files(v) for v in value]
    if isinstance(value, dict):
        return {k: inline_files(v) for k, v in value.items()}
    return value


def urls(value, found):
    if isinstance(value, dict):
        for k, v in value.items():
            if k == "url" and isinstance(v, str) and v.startswith("http"):
                found.append((value.get("file_name") or v.split("?")[0].rsplit("/", 1)[-1], v))
            else:
                urls(v, found)
    elif isinstance(value, list):
        for v in value:
            urls(v, found)


def main():
    endpoint, args, out_dir, name = sys.argv[1:5]
    if endpoint == "fetch":
        # Resume a job submitted earlier: args is its response_url (printed as "submitted ...").
        response_url = args
        status_url = response_url + "/status"
    else:
        body = json.load(open(args)) if os.path.exists(args) else json.loads(args)
        submitted = request("https://queue.fal.run/" + endpoint, inline_files(body), tries=3)
        status_url, response_url = submitted["status_url"], submitted["response_url"]
        print("submitted", response_url, flush=True)
    started = time.time()
    while True:
        status = request(status_url)
        if status.get("status") == "COMPLETED":
            break
        if time.time() - started > 1800:
            sys.exit("timed out: " + json.dumps(status)[:300])
        time.sleep(4)
    result = request(response_url)
    found = []
    urls(result, found)
    os.makedirs(out_dir, exist_ok=True)
    seen = set()
    for i, (file_name, url) in enumerate(found):
        if url in seen:
            continue            # Tripo lists its mesh under several keys
        seen.add(url)
        ext = os.path.splitext(file_name)[1] or ".bin"
        path = os.path.join(out_dir, name + ("" if len(found) == 1 else "-%d" % i) + ext)
        download(url, path)
        print("saved", path)
    brief = json.dumps(result)
    print("result", brief[:400] if len(brief) > 400 else brief)


if __name__ == "__main__":
    main()

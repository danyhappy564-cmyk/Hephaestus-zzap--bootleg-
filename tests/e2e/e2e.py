# End-to-end check against a local headless SPT 4.1 server (see README). Usage: python3 e2e.py <server output folder>
"""End-to-end check of the Hephaestus mod against a local headless SPT 4.1 server (test only, 127.0.0.1)."""
import json, ssl, sys, zlib, urllib.request, os

HOST = "https://127.0.0.1:6969"
TRADER = "a18160fc82b4f2be55205c89"
ROUBLES = "5449016a4bdc2d6f028b456f"
EUROS = "569668774bdc2da2298b4568"
SPT = sys.argv[1]  # server output folder
CTX = ssl.create_default_context()
CTX.check_hostname = False
CTX.verify_mode = ssl.CERT_NONE  # local self-signed test server only

passed = failed = 0
def check(ok, name, extra=""):
    global passed, failed
    if ok: passed += 1; print("PASS", name)
    else: failed += 1; print("FAIL", name, extra)

def req(path, body=None, sid=None, plain=False, raw=False):
    data = json.dumps(body if body is not None else {}).encode()
    headers = {"Content-Type": "application/json"}
    if plain:
        headers.update({"requestcompressed": "0", "responsecompressed": "0"})
    else:
        data = zlib.compress(data)
    if sid: headers["Cookie"] = "PHPSESSID=" + sid
    r = urllib.request.urlopen(urllib.request.Request(HOST + path, data=data, headers=headers, method="POST"), context=CTX, timeout=60)
    content = r.read()
    if raw: return r.status, r.headers.get("Content-Type"), content
    if not plain:
        try: content = zlib.decompress(content)
        except zlib.error: pass
    return json.loads(content.decode("utf-8"))

def get(path, sid=None):
    headers = {}
    if sid: headers["Cookie"] = "PHPSESSID=" + sid
    r = urllib.request.urlopen(urllib.request.Request(HOST + path, headers=headers), context=CTX, timeout=60)
    return r.status, r.headers.get("Content-Type"), r.read()

def account(name):
    reg = req("/launcher/v2/register", {"username": name, "password": "", "edition": "Standard"})
    sid = next(p["profileId"] for p in reg["Profiles"] if p["username"] == name)
    req("/launcher/v2/login", {"username": name, "password": ""})
    created = req("/client/game/profile/create", {"side": "Usec", "nickname": name, "headId": "5cde96047d6c8b20b577f016", "voiceId": "5fc1223595572123ae7384a3"}, sid)
    print("create:", str(created)[:200])
    return sid

def pmc(sid):
    lst = req("/client/game/profile/list", {}, sid)["data"]
    return next(p for p in lst if p.get("Info", {}).get("Side") in ("Usec", "Bear"))

# ---------- settings routes (F12 plugin uses plain JSON) ----------
s = req("/hephaestus/settings/get", {}, plain=True)
check(s.get("ok") and s["settings"]["currency"] == EUROS and s["settings"]["discount"] == 90, "F12 get route: server config", s)

# ---------- two accounts ----------
sidA = account("hephA")
sidB = account("hephB")
check(isinstance(sidA, str) and len(sidA) == 24, "account A session id", sidA)

# ---------- trader visible ----------
ts = req("/client/trading/api/traderSettings", {}, sidA)["data"]
mine = [t for t in ts if t["_id"] == TRADER]
check(len(mine) == 1, "trader in traderSettings")
if mine:
    check(mine[0].get("isAvailableInPVE") is True, "isAvailableInPVE true in what the client receives")
    check(mine[0].get("avatar") == "/files/trader/avatar/hephaestus_alxk.jpg", "avatar path sent to client")

code, ctype, img = get("/files/trader/avatar/hephaestus_alxk.jpg", sidA)
check(code == 200 and len(img) > 1000 and img[:2] == b"\xff\xd8", "portrait served", (code, ctype, len(img)))

# ---------- locales ----------
for lang, want in (("kr", "헤파이스토스"), ("en", "Hephaestus"), ("ru", "Hephaestus")):
    loc = req("/client/locale/" + lang, {}, sidA)["data"]
    check(loc.get(f"{TRADER} Nickname") == want and loc.get(f"{TRADER} Description"), f"locale {lang}: nickname {want}", loc.get(f"{TRADER} Nickname"))
check("할인" in req("/client/locale/kr", {}, sidA)["data"][f"{TRADER} Description"], "kr description is Korean")

# ---------- empty assort before any build ----------
a0 = req("/client/trading/api/getTraderAssort/" + TRADER, {}, sidA)["data"]
check(a0["items"] == [] and a0["barter_scheme"] == {}, "no builds -> empty assort")

# ---------- save a weapon build (a vanilla default preset) ----------
globs = json.load(open(os.path.join(SPT, "SPT_Data/database/globals.json")))
presets = [p for p in globs["ItemPresets"].values() if p.get("_encyclopedia") and len(p["_items"]) >= 6]
preset = presets[0]
items = preset["_items"]
build_id = "6720aaaa0000000000000001"
root_id = preset["_parent"]
build = {"Id": build_id, "Name": "test build", "Root": root_id, "Items": items}
print("     builds list (init):", str(req("/client/builds/list", {}, sidA))[:120])
save = req("/client/builds/weapon/save", build, sidA)
print("save build:", str(save)[:150])
builds_before = req("/client/builds/list", {}, sidA)["data"]["weaponBuilds"]
check(len(builds_before) == 1, "build saved in profile A")

# ---------- assort for A ----------
a1 = req("/client/trading/api/getTraderAssort/" + TRADER, {}, sidA)["data"]
roots = [i for i in a1["items"] if i.get("parentId") == "hideout"]
check(len(roots) == 1 and len(a1["items"]) == len(items), "A: one offer with every part", (len(roots), len(a1["items"]), len(items)))
offer = roots[0] if roots else {}
check(offer.get("upd", {}).get("UnlimitedCount") is True and offer.get("upd", {}).get("StackObjectsCount") == 2000, "offer: unlimited, 2000 stock")
build_ids = {i["_id"] for i in items}
check(all(i["_id"] not in build_ids for i in a1["items"]), "offer ids differ from build ids")
scheme = a1["barter_scheme"].get(offer.get("_id"), [[{}]])[0][0]
check(scheme.get("_tpl") == EUROS and scheme.get("count", 0) >= 1 and float(scheme["count"]).is_integer(), "price in EUR, whole number", scheme)
check(a1["loyal_level_items"].get(offer.get("_id")) == 1, "loyalty level 1")
a1b = req("/client/trading/api/getTraderAssort/" + TRADER, {}, sidA)["data"]
check(a1b["barter_scheme"] == a1["barter_scheme"] and [i["_id"] for i in a1b["items"]] == [i["_id"] for i in a1["items"]], "same price and ids on reopen (no random)")
print("     offer price:", scheme)

builds_after = req("/client/builds/list", {}, sidA)["data"]["weaponBuilds"]
check(builds_after == builds_before, "profile build unchanged after building the assort")

# ---------- B does not see A's build ----------
b1 = req("/client/trading/api/getTraderAssort/" + TRADER, {}, sidB)["data"]
check(b1["items"] == [] and b1["barter_scheme"] == {}, "B does not see A's build")
a2 = req("/client/trading/api/getTraderAssort/" + TRADER, {}, sidA)["data"]
check(len(a2["items"]) == len(items), "A still sees its build after B opened the trader")

# ---------- switch to roubles via F12 route, buy ----------
s2 = req("/hephaestus/settings/set", {"currency": ROUBLES, "discount": 90, "cost": 123, "debugLogging": True}, plain=True)
check(s2.get("ok") and s2["settings"]["currency"] == ROUBLES and s2["messageKo"].startswith("저장"), "F12 set route: roubles", s2)
cfg = json.load(open(os.path.join(SPT, "user/mods/Hephaestus/config/config.json")))
check(cfg["currency"] == ROUBLES and cfg["debugLogging"] is True and "HELP_discount" in cfg, "config.json saved, HELP keys kept")

a3 = req("/client/trading/api/getTraderAssort/" + TRADER, {}, sidA)["data"]
offer = [i for i in a3["items"] if i.get("parentId") == "hideout"][0]
scheme = a3["barter_scheme"][offer["_id"]][0][0]
check(scheme["_tpl"] == ROUBLES, "price now in roubles", scheme)
price = int(scheme["count"])
print("     rouble price:", price)

prof = pmc(sidA)
inv = prof["Inventory"]["items"]
money = [i for i in inv if i["_tpl"] == ROUBLES]
total = sum(i.get("upd", {}).get("StackObjectsCount", 0) for i in money)
print("     roubles in stash:", total, "stacks:", len(money))
check(total >= price, "enough roubles to buy", (total, price))
before_weapons = sum(1 for i in inv if i["_tpl"] == offer["_tpl"])

# pay from stacks
pay, left = [], price
for m in sorted(money, key=lambda m: -m.get("upd", {}).get("StackObjectsCount", 0)):
    take = min(left, m.get("upd", {}).get("StackObjectsCount", 0))
    if take > 0: pay.append({"id": m["_id"], "count": take}); left -= take
    if left <= 0: break

buy = req("/client/game/profile/items/moving", {"data": [{"Action": "TradingConfirm", "type": "buy_from_trader", "tid": TRADER,
          "item_id": offer["_id"], "count": 1, "scheme_id": 0, "scheme_items": pay}], "tm": 0, "reload": 0}, sidA)
warn = buy.get("data", {}).get("warnings") if isinstance(buy.get("data"), dict) else None
check(buy.get("err") == 0 and not warn, "purchase accepted", str(buy)[:400])

prof2 = pmc(sidA)
inv2 = prof2["Inventory"]["items"]
after_weapons = [i for i in inv2 if i["_tpl"] == offer["_tpl"]]
check(len(after_weapons) == before_weapons + 1, "bought weapon is in the stash")
total2 = sum(i.get("upd", {}).get("StackObjectsCount", 0) for i in inv2 if i["_tpl"] == ROUBLES)
check(total - total2 == price, "paid exactly the shown price", (total, total2, price))
if after_weapons:
    w = after_weapons[-1]
    def children(pid): return [i for i in inv2 if i.get("parentId") == pid]
    tree, stack = [], [w["_id"]]
    while stack and len(tree) < 500:
        x = stack.pop(); tree.append(x); stack += [c["_id"] for c in children(x)]
    check(len(tree) == len(items), "bought weapon has every part", (len(tree), len(items)))
    ids2 = [i["_id"] for i in inv2]
    check(len(ids2) == len(set(ids2)), "no duplicate item ids in the stash")
    offer_ids = {i["_id"] for i in a3["items"]}
    check(not (set(tree) & offer_ids) and not (set(tree) & build_ids), "bought parts got new ids")

# buy a second one: ids must not collide
prof3 = pmc(sidA); inv3 = prof3["Inventory"]["items"]
money = [i for i in inv3 if i["_tpl"] == ROUBLES]
pay, left = [], price
for m in sorted(money, key=lambda m: -m.get("upd", {}).get("StackObjectsCount", 0)):
    take = min(left, m.get("upd", {}).get("StackObjectsCount", 0))
    if take > 0: pay.append({"id": m["_id"], "count": take}); left -= take
    if left <= 0: break
if left <= 0:
    buy2 = req("/client/game/profile/items/moving", {"data": [{"Action": "TradingConfirm", "type": "buy_from_trader", "tid": TRADER,
               "item_id": offer["_id"], "count": 1, "scheme_id": 0, "scheme_items": pay}], "tm": 0, "reload": 0}, sidA)
    inv4 = pmc(sidA)["Inventory"]["items"]
    ids4 = [i["_id"] for i in inv4]
    check(buy2.get("err") == 0 and sum(1 for i in inv4 if i["_tpl"] == offer["_tpl"]) == before_weapons + 2 and len(ids4) == len(set(ids4)),
          "second purchase works, still no duplicate ids")
else:
    print("     (not enough roubles for a second purchase, skipped)")

# restore the shipped settings
req("/hephaestus/settings/set", {"currency": EUROS, "discount": 90, "cost": 123, "debugLogging": False}, plain=True)

print(f"\n{passed} passed, {failed} failed")
sys.exit(1 if failed else 0)

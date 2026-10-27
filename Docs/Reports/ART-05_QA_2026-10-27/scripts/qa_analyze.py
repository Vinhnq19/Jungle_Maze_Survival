import json, math, sys
r = json.load(open(sys.argv[1], encoding='utf-8'))
fails = []
print('%-14s %2s %-9s %-9s %6s %6s %6s %6s %6s %5s %6s %4s' % ('sfx','n','len min','len max','peak','Lmin','Lmax','vol','E','tier','head','run'))
star = {'MeleeSwing','MeleeHit','ShootRanged','ShootCrossbow','HitMarker','HurtLocal','PickupWeapon','PickupItem','WearArmor','ZoneBell3','DragonRoar'}
Eof = {}
for name, s in r['sfx'].items():
    cl = s['clips']; lo, hi = s['len_range']; tier = s['tier']
    durs = [c['dur'] for c in cl]; peaks = [c['peak_db'] for c in cl]; Ls = [c['L_db'] for c in cl]
    L = s['L_mean']; vol = s['entry']['volume']; E = s['E']; Eof[name] = E
    heads = max(c['head_db'] for c in cl); tails = max(c['tail_db'] for c in cl); run = max(c['ceil_run'] for c in cl)
    print('%-14s %2d %-9.3f %-9.3f %6.2f %6.2f %6.2f %6.2f %6.2f %5d %6.1f %4d' % (name, len(cl), min(durs), max(durs), max(peaks), min(Ls), max(Ls), vol, E, tier, max(heads,tails), run))
    if any(c['ch'] != 1 for c in cl): fails.append(name+': not mono')
    if any(c['sr'] != 44100 for c in cl): fails.append(name+': sr')
    if min(durs) < lo - 1e-9 or max(durs) > hi + 1e-9: fails.append('%s: length %.3f-%.3f outside %.2f-%.2f' % (name, min(durs), max(durs), lo, hi))
    if max(peaks) > -1.0: fails.append('%s: peak %.2f > -1' % (name, max(peaks)))
    if run >= 3: fails.append('%s: ceiling run %d' % (name, run))
    if heads > -40 or tails > -40: fails.append('%s: edge head %.1f tail %.1f' % (name, max(c['head_db'] for c in cl), max(c['tail_db'] for c in cl)))
    lrange = (-26, -22) if name.startswith('Footstep') else (-20, -15)
    for c in cl:
        if not (lrange[0] <= c['L_db'] <= lrange[1]): fails.append('%s: %s L %.2f outside %s' % (name, c['path'].split('/')[-1], c['L_db'], lrange))
    if abs(E - tier) > 3: fails.append('%s: E %.2f vs tier %d' % (name, E, tier))
    if vol > 1.0: fails.append(name+': volume>1')
t = [n for n in Eof if n not in ('HitMarker',) and r['sfx'][n]['tier'] <= -20]
mx = max(t, key=lambda n: Eof[n])
print('HitMarker E %.2f; max E among T2-T4: %s %.2f -> margin %.2f; vs FootstepGrass %.2f' % (Eof['HitMarker'], mx, Eof[mx], Eof['HitMarker']-Eof[mx], Eof['HitMarker']-Eof['FootstepGrass']))
print('beep refs:', r['beep_refs']); print('imported guid refs:', r['all_guids_in_imported'])
print('music:'); 
for k,v in r['music'].items(): print(' ', k, v['path'].split('/')[-1], '%.2fs' % v['dur'], v['size'], 'peak %.2f L %.2f ch %d sr %d' % (v['peak_db'], v['L_db'], v['ch'], v['sr']))
print('FAILS:', len(fails)); [print(' ', f) for f in fails]

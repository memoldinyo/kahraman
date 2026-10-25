"""Prüft die Sprachdateien einer Sprache gegen die deutschen Vorlagen.

Aufruf:  python -I -X utf8 check.py <code>      (z. B. tr)
Prüft ui.<code>.json, quips.<code>.json und help.<code>.txt im Ordner src/Kahraman.Core/i18n.
"""
import json, os, re, sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'src', 'Kahraman.Core', 'i18n')
code = sys.argv[1]
errors = []

def load(name):
    with open(os.path.join(ROOT, name), encoding='utf-8') as f:
        return json.load(f)

def width(s):
    return sum(2 if '⺀' <= c <= '￯' else 1 for c in s)

def mnemonics(s):
    return s.replace('&&', '').count('&')

# --- ui ---
de = load('ui.de.json')
try:
    ui = load(f'ui.{code}.json')
except Exception as e:
    errors.append(f'ui.{code}.json: {e}'); ui = {}
for k in de:
    if k not in ui:
        errors.append(f'ui: Schlüssel fehlt: {k}')
for k in ui:
    if k not in de:
        errors.append(f'ui: überzähliger Schlüssel: {k}')
ph = re.compile(r'\{(\d+)(:[^}]*)?\}')
tok = re.compile(r'\$(YES|NO|CANCEL)\$')
for k, v in ui.items():
    if k not in de: continue
    if not v.strip(): errors.append(f'ui: leer: {k}')
    if set(m.group(0) for m in ph.finditer(de[k])) != set(m.group(0) for m in ph.finditer(v)):
        errors.append(f'ui: Platzhalter {{0}}/{{1:G}}... anders als im Deutschen: {k}: {v!r}')
    if set(tok.findall(de[k])) != set(tok.findall(v)):
        errors.append(f'ui: $YES$/$NO$/$CANCEL$ anders als im Deutschen: {k}')
    if mnemonics(de[k]) != mnemonics(v):
        errors.append(f'ui: Anzahl & (Tastenkürzel) anders als im Deutschen ({mnemonics(de[k])}): {k}: {v!r}')
    stripped = ph.sub('', v)
    if '{' in stripped or '}' in stripped:
        errors.append(f'ui: geschweifte Klammer außerhalb eines Platzhalters: {k}')

# --- quips ---
try:
    q = load(f'quips.{code}.json')
except Exception as e:
    errors.append(f'quips.{code}.json: {e}'); q = {}
conds = set(load('quips.de.json')['situational'].keys())
def entries(name):
    v = q.get(name, [])
    if not v: errors.append(f'quips: {name} fehlt oder ist leer')
    for e in v:
        if not (isinstance(e, list) and len(e) == 2 and isinstance(e[0], int) and isinstance(e[1], str)):
            errors.append(f'quips: {name}: Eintrag muss [Gewicht, "Text"] sein: {e!r}')
    return [e for e in v if isinstance(e, list) and len(e) == 2]
questions = entries('questions'); sayings = entries('sayings'); last = entries('lastActivity')
titles = entries('titles'); oks = entries('okTexts')
sit = q.get('situational', {})
for name in sit:
    if name not in conds: errors.append(f'quips: unbekannte Bedingung {name} (erlaubt: {sorted(conds)})')
facts = q.get('facts', []); intros = q.get('factIntros', []); follow = q.get('factFollowUps', [])
fmt = q.get('factFormat', '{0} {1} {2}')
for n, v in (('facts', facts), ('factIntros', intros), ('factFollowUps', follow)):
    if not v: errors.append(f'quips: {n} fehlt oder ist leer')
texts = [e[1] for e in questions + sayings]
texts += [e[1] for g in sit.values() for e in g]
texts += [e[1].replace('@LAST@', 'X' * 29 + '…') for e in last]
texts += [fmt.format(i, f, u) for f in facts for i in intros for u in follow]
for t in texts:
    if width(t) > 80: errors.append(f'quips: zu lang ({width(t)} > 80, chinesische Zeichen zählen doppelt): {t!r}')
for e in last:
    if '@LAST@' not in e[1]: errors.append(f'quips: lastActivity ohne @LAST@: {e[1]!r}')
for e in oks:
    if width(e[1].replace('&', '')) > 12: errors.append(f'quips: OK-Text zu lang (> 12): {e[1]!r}')
    if e[1].count('&') != 1: errors.append(f'quips: OK-Text braucht genau ein &: {e[1]!r}')
for e in titles:
    if width(e[1]) > 60: errors.append(f'quips: Titel zu lang (> 60): {e[1]!r}')
plain = sum(e[0] for e in questions)
others = sum(e[0] for e in sayings) + 2 * len(facts)
if not (0.15 <= plain / (plain + others) <= 0.5):
    errors.append(f'quips: Gewichte: normale Fragen sollen 15–50 % ausmachen (jetzt {plain}/{plain + others})')

# --- help ---
try:
    help_text = open(os.path.join(ROOT, f'help.{code}.txt'), encoding='utf-8').read()
    if 'taetigkeiten.kahraman' not in help_text: errors.append('help: Dateiname taetigkeiten.kahraman muss unverändert vorkommen')
except Exception as e:
    errors.append(f'help.{code}.txt: {e}')

if errors:
    print(f'{len(errors)} Problem(e):'); print('\n'.join(errors)); sys.exit(1)
print(f'{code}: alles in Ordnung ({len(ui)} Texte, {len(texts)} Sprüche geprüft)')

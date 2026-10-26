"""Dump GDD workbook sheets as tab-separated text using only the standard library."""
from pathlib import Path
import argparse
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GDD = ROOT / 'Docs/[GDD] - Lastaway.xlsx'
NS = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main',
      'r': 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
REL_NS = '{http://schemas.openxmlformats.org/package/2006/relationships}'


def column_index(ref):
    letters = re.match(r'[A-Z]+', ref).group(0)
    index = 0
    for letter in letters:
        index = index * 26 + ord(letter) - ord('A') + 1
    return index - 1


def text_of(node):
    return ''.join(t.text or '' for t in node.iter(f"{{{NS['m']}}}t"))


def load_sheets(book):
    workbook = ET.fromstring(book.read('xl/workbook.xml'))
    rels = ET.fromstring(book.read('xl/_rels/workbook.xml.rels'))
    targets = {rel.get('Id'): rel.get('Target') for rel in rels.iter(f'{REL_NS}Relationship')}
    sheets = {}
    for sheet in workbook.iter(f"{{{NS['m']}}}sheet"):
        target = targets[sheet.get(f"{{{NS['r']}}}id")].lstrip('/')
        sheets[sheet.get('name')] = target if target.startswith('xl/') else 'xl/' + target
    return sheets


def read_rows(book, path, shared):
    root = ET.fromstring(book.read(path))
    for row in root.iter(f"{{{NS['m']}}}row"):
        values = {}
        for cell in row.iter(f"{{{NS['m']}}}c"):
            kind = cell.get('t')
            value = cell.find('m:v', NS)
            if kind == 's' and value is not None:
                text = shared[int(value.text)]
            elif kind == 'inlineStr':
                text = text_of(cell)
            else:
                text = value.text if value is not None else ''
            if text:
                values[column_index(cell.get('r'))] = ' '.join(text.split())
        if values:
            yield [values.get(i, '') for i in range(max(values) + 1)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('sheet', nargs='?', help='Sheet name; omit to list sheets')
    parser.add_argument('--file', type=Path, default=DEFAULT_GDD)
    parser.add_argument('--grep', help='Only print rows containing this text (case-insensitive)')
    parser.add_argument('--id', help='Only print rows whose first column equals this value (e.g. a task ID)')
    args = parser.parse_args()

    with zipfile.ZipFile(args.file) as book:
        sheets = load_sheets(book)
        if not args.sheet:
            print('\n'.join(sheets))
            return
        if args.sheet not in sheets:
            sys.exit(f'Unknown sheet {args.sheet!r}. Available: {", ".join(sheets)}')
        shared = []
        if 'xl/sharedStrings.xml' in book.namelist():
            shared = [text_of(si) for si in ET.fromstring(book.read('xl/sharedStrings.xml')).iter(f"{{{NS['m']}}}si")]
        needle = args.grep.lower() if args.grep else None
        sys.stdout.reconfigure(encoding='utf-8')
        for row in read_rows(book, sheets[args.sheet], shared):
            line = '\t'.join(row)
            if args.id is not None and row[0].lower() != args.id.lower():
                continue
            if needle is None or needle in line.lower():
                print(line)


if __name__ == '__main__':
    main()

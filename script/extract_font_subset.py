import os
import re
import subprocess
import sys


def find_used_icons(project_dir):
    used_icons = set()

    for root, dirs, files in os.walk(project_dir):
        dirs[:] = [d for d in dirs if d not in ('bin', 'obj', '.git')]

        for file in files:
            filepath = os.path.join(root, file)
            if 'IconConverter.cs' in filepath:
                continue
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()

                matches = re.findall(r'IconTypes\.(\w+)', content)
                used_icons.update(matches)

                if file.endswith('.axaml'):
                    matches = re.findall(r'(?:Icon|SelectedIcon|IconType)="(\w+)"', content)
                    used_icons.update(matches)
            except:
                pass

    return used_icons


def get_icon_unicode_map(iconconverter_path):
    with open(iconconverter_path, 'r', encoding='utf-8') as f:
        content = f.read()

    pattern = r'\{ IconTypes\.(\w+),\s*"\\x([0-9a-fA-F]+)"'
    matches = re.findall(pattern, content)

    return {name: int(code, 16) for name, code in matches}


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.normpath(os.path.join(script_dir, '..'))

    input_font = os.path.join(script_dir, 'Segoe Fluent Icons.ttf')
    output_font = os.path.join(project_root, 'Taix.Client', 'Resources', 'Fonts', 'SegoeFluentIcons-Subset.ttf')
    iconconverter_path = os.path.join(project_root, 'Taix.Client', 'Controls', 'Base', 'IconConverter.cs')

    print("=" * 50)
    print("Font Subset Extractor")
    print("=" * 50)

    print("\n[1/3] Scanning project for used icons...")
    used_icons = find_used_icons(os.path.join(project_root, 'Taix.Client'))
    used_icons.discard('None')
    print(f"      Found {len(used_icons)} icons")

    print("\n[2/3] Getting Unicode mappings...")
    icon_map = get_icon_unicode_map(iconconverter_path)

    codepoints = set()
    missing = []
    for icon in used_icons:
        if icon in icon_map:
            codepoints.add(icon_map[icon])
        else:
            missing.append(icon)

    if missing:
        print(f"      Warning: {len(missing)} icons missing mapping: {missing[:5]}...")

    print(f"      Need to subset {len(codepoints)} glyphs")

    print("\n[3/3] Generating subset font...")

    temp_unicodes = os.path.join(script_dir, '_temp_unicodes.txt')
    with open(temp_unicodes, 'w') as f:
        for cp in sorted(codepoints):
            f.write(f'{cp:04X}\n')

    cmd = [
        'pyftsubset',
        input_font,
        f'--unicodes-file={temp_unicodes}',
        f'--output-file={output_font}',
        '--name-IDs=*',
        '--name-legacy',
        '--name-languages=*',
    ]

    result = subprocess.run(cmd, capture_output=True, text=True)

    if os.path.exists(temp_unicodes):
        os.remove(temp_unicodes)

    if result.returncode != 0:
        print(f"[ERROR] pyftsubset failed: {result.stderr}")
        return 1

    input_size = os.path.getsize(input_font)
    output_size = os.path.getsize(output_font)
    reduction = (1 - output_size / input_size) * 100

    print(f"\n{'=' * 50}")
    print("[SUCCESS]")
    print(f"{'=' * 50}")
    print(f"Input:  {input_size:,} bytes ({input_size/1024:.1f} KB)")
    print(f"Output: {output_size:,} bytes ({output_size/1024:.1f} KB)")
    print(f"Reduced: {reduction:.1f}%")
    print(f"Glyphs: {len(codepoints)}")

    return 0


if __name__ == '__main__':
    sys.exit(main())

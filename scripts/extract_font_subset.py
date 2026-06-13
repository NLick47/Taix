#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Font Subset Extractor - Extract only needed glyphs from Segoe Fluent Icons
Usage: python extract_font_subset.py
"""

import os
import subprocess
import sys

# Unicode codepoints needed for Taix app
CODEPOINTS = [
    0xE8FB,  # Accept
    0xF0AE,  # ArrowDown8
    0xF0AD,  # ArrowUp8
    0xE72B,  # Back
    0xE8FD,  # BulletedList
    0xE948,  # CalculatorAddition
    0xE70D,  # ChevronDown
    0xE70E,  # ChevronUp
    0xE783,  # Error
    0xE8B7,  # Folder
    0xE8D5,  # FolderFill
    0xE80F,  # Home
    0xEA8A,  # HomeSolid
    0xEDB1,  # ImportantBadge12
    0xE814,  # IncidentTriangle
    0xE946,  # Info
    0xE712,  # More
    0xF8AB,  # SubtractBold
    0xE904,  # ZeroBars
]


def main():
    # Paths
    script_dir = os.path.dirname(os.path.abspath(__file__))
    input_font = os.path.normpath(os.path.join(
        script_dir, '..', 'Taix.Client', 'Resources', 'Fonts', 'Segoe Fluent Icons.ttf'
    ))
    output_font = os.path.normpath(os.path.join(
        script_dir, '..', 'Taix.Client', 'Resources', 'Fonts', 'SegoeFluentIcons-Subset.woff2'
    ))

    print("=" * 50)
    print("Font Subset Extractor")
    print("=" * 50)
    print(f"\nInput:  {input_font}")
    print(f"Output: {output_font}")
    print(f"\nNeeded codepoints ({len(CODEPOINTS)}):")
    for cp in CODEPOINTS:
        print(f"  U+{cp:04X}")

    if not os.path.exists(input_font):
        print(f"\n[ERROR] Input font not found")
        return 1

    # Build unicode list
    unicodes = ','.join(f'{cp:X}' for cp in CODEPOINTS)

    # Run pyftsubset
    print("\nRunning pyftsubset...")
    cmd = [
        'pyftsubset',
        input_font,
        f'--unicodes={unicodes}',
        f'--output-file={output_font}',
        '--flavor=woff2'
    ]

    result = subprocess.run(cmd, capture_output=True, text=True)

    if result.returncode != 0:
        print(f"[ERROR] pyftsubset failed: {result.stderr}")
        return 1

    # Stats
    input_size = os.path.getsize(input_font)
    output_size = os.path.getsize(output_font)
    reduction = (1 - output_size / input_size) * 100

    print(f"\n{'=' * 50}")
    print("[SUCCESS]")
    print(f"{'=' * 50}")
    print(f"Input:  {input_size:,} bytes ({input_size/1024:.1f} KB)")
    print(f"Output: {output_size:,} bytes ({output_size/1024:.1f} KB)")
    print(f"Saved:  {input_size - output_size:,} bytes")
    print(f"Reduction: {reduction:.1f}%")

    return 0


if __name__ == '__main__':
    sys.exit(main())

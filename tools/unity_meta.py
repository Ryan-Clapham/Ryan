#!/usr/bin/env python3
"""Creates missing Unity .meta files (with fresh GUIDs) for every file and folder under ColourCascade/Assets.

Unity would generate these itself on first import; committing them keeps GUIDs stable across machines.
Usage: python3 tools/unity_meta.py
"""
import os
import sys
import uuid

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ColourCascade", "Assets")

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

SCRIPT = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

ASMDEF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

DEFAULT = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FONT = """fileFormatVersion: 2
guid: {guid}
TrueTypeFontImporter:
  externalObjects: {{}}
  serializedVersion: 4
  fontSize: 16
  forceTextureCase: -2
  characterSpacing: 0
  characterPadding: 1
  includeFontData: 1
  fontNames:
  - Fredoka
  fallbackFontReferences: []
  customCharacters: 
  fontRenderingMode: 0
  ascentCalculationMode: 1
  useLegacyBoundsCalculation: 0
  shouldRoundAdvanceValue: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

TEMPLATES = {
    ".cs": SCRIPT,
    ".asmdef": ASMDEF,
    ".txt": TEXT,
    ".md": TEXT,
    ".json": TEXT,
    ".ttf": FONT,
    ".unity": DEFAULT,
}


def write_meta(path, template):
    meta = path + ".meta"
    if os.path.exists(meta):
        return False
    with open(meta, "w", newline="\n") as f:
        f.write(template.format(guid=uuid.uuid4().hex))
    return True


def main():
    created = 0
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if not d.startswith(".")]
        for d in dirnames:
            created += write_meta(os.path.join(dirpath, d), FOLDER)
        for name in filenames:
            if name.endswith(".meta") or name.startswith("."):
                continue
            ext = os.path.splitext(name)[1].lower()
            created += write_meta(os.path.join(dirpath, name), TEMPLATES.get(ext, DEFAULT))
    # The top-level asset folder itself.
    created += write_meta(os.path.join(ROOT, "ColourCascade"), FOLDER) if os.path.isdir(os.path.join(ROOT, "ColourCascade")) else 0
    print("Created %d .meta files" % created)
    return 0


if __name__ == "__main__":
    sys.exit(main())

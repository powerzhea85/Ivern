from __future__ import annotations

import base64
import gzip
import hashlib
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1] if pathlib.Path(__file__).parent.name == 'tools' else pathlib.Path.cwd()
TOOLS = ROOT / 'tools'


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def run(*args: str) -> None:
    print('+', ' '.join(args), flush=True)
    subprocess.check_call(args, cwd=str(ROOT))


def write_decoded_b64_gzip(source: pathlib.Path, destination: pathlib.Path, expected_sha: str) -> None:
    encoded = source.read_text(encoding='ascii').strip()
    decoded = gzip.decompress(base64.b64decode(encoded))
    actual = sha256_bytes(decoded)
    if actual != expected_sha:
        raise RuntimeError(f'{destination.name} hash mismatch: {actual}')
    destination.write_bytes(decoded)


def reconstruct_parts(directory: str, count: int, destination: str, encoded_sha: str, decoded_sha: str) -> pathlib.Path:
    parts = sorted((TOOLS / directory).glob('part*.txt'))
    if len(parts) != count:
        raise RuntimeError(f'Expected {count} chunks in {directory}, found {len(parts)}')
    encoded = ''.join(p.read_text(encoding='ascii').strip() for p in parts)
    actual_encoded = sha256_bytes(encoded.encode('ascii'))
    if actual_encoded != encoded_sha:
        raise RuntimeError(f'{destination} encoded hash mismatch: {actual_encoded}')
    decoded = gzip.decompress(base64.b64decode(encoded))
    actual_decoded = sha256_bytes(decoded)
    if actual_decoded != decoded_sha:
        raise RuntimeError(f'{destination} decoded hash mismatch: {actual_decoded}')
    path = TOOLS / destination
    path.write_bytes(decoded)
    return path


def apply_patch(path: pathlib.Path) -> None:
    run('git', 'apply', '--check', '--verbose', '--whitespace=nowarn', str(path.relative_to(ROOT)))
    run('git', 'apply', '--verbose', '--whitespace=nowarn', str(path.relative_to(ROOT)))


def normalize_text_sources() -> None:
    roots = [ROOT / 'src' / 'BenjaminMenu', ROOT / 'docs']
    suffixes = {'.cs', '.csproj', '.md'}
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob('*'):
            if path.is_file() and path.suffix.lower() in suffixes:
                data = path.read_bytes().replace(b'\r\n', b'\n')
                path.write_bytes(data)


def lf_copy(source: pathlib.Path, destination: pathlib.Path) -> pathlib.Path:
    destination.write_bytes(source.read_bytes().replace(b'\r\n', b'\n'))
    return destination


def main() -> int:
    (ROOT / '.github' / 'workflows').mkdir(parents=True, exist_ok=True)
    run(sys.executable, 'tools/apply_wave2.py')

    wave2_hf1 = TOOLS / 'wave2-hotfix1.patch'
    write_decoded_b64_gzip(TOOLS / 'wave2-hotfix1.patch.gz.b64', wave2_hf1, '7d1495fda42851e38ddda9fff4c5fd35d9f97258d75e6915c74721f817bc2c07')
    apply_patch(wave2_hf1)

    apply_patch(reconstruct_parts('hf2parts', 7, 'wave2-hotfix2.patch', 'ec12003bcffaaf0ac4792d71ce326380958a4f82ba594a124daebdb429468176', '4602cfb199bfcd6dbb366a4434c78358342cb0b14ad1880536ac2948bb454969'))
    apply_patch(reconstruct_parts('hf3parts', 2, 'wave2-hotfix3.patch', '5402d8d102fb9d27f0cc67511f951dcefdf695170257b75284ccfb8d48d0cdb5', 'ca2d4d3ded5a527576b6be7cdb5a5e0179a16e9330880109b227ac916a4adf1d'))
    apply_patch(reconstruct_parts('hf4parts', 2, 'wave2-hotfix4.patch', 'a8e713b302ddd592632198527dd77271a1a5a0320873c194ee2ecd8a30489647', 'eed30e823d7ea7b5a6f6b9fc07af830e8235fe43e26d461a21c203182557e26d'))
    apply_patch(reconstruct_parts('wave3parts', 1, 'wave3-grenade.patch', 'fd637db0519c8f8c2c3ac7d47ee986baabe27d953e4d56770226799a28b537ad', '0705d74b53a6473fa88dab006dc6190aebf830d42f3f491af90d8e1ff69f9bfe'))

    grenade_source = ROOT / 'src' / 'BenjaminMenu' / 'Overlay' / 'GrenadeTrajectoryModule.cs'
    grenade_source.write_bytes(grenade_source.read_bytes().replace(b'\r\n', b'\n'))
    apply_patch(lf_copy(TOOLS / 'wave3-compilefix.patch', TOOLS / 'wave3-compilefix.lf.patch'))

    parts = sorted((TOOLS / 'wave4patch').glob('part*.txt'))
    if len(parts) != 10:
        raise RuntimeError(f'Expected 10 Wave 4 chunks, found {len(parts)}')
    patch_bytes = b''.join(p.read_bytes() for p in parts)
    boundary = b' }\ndiff --git a/src/BenjaminMenu/Gameplay/GameplayManager.cs'
    if patch_bytes.count(boundary) != 1:
        raise RuntimeError('Wave 4 CRLF boundary not found exactly once')
    patch_bytes = patch_bytes.replace(boundary, b' }\r\ndiff --git a/src/BenjaminMenu/Gameplay/GameplayManager.cs')
    if sha256_bytes(patch_bytes) != '4a68e5746557e18e55c25810e1277152ec1178f1a909bf240ad7ec7b6ba20485':
        raise RuntimeError('Wave 4 weapon patch hash mismatch')
    wave4_weapon = TOOLS / 'wave4-weapon.patch'
    wave4_weapon.write_bytes(patch_bytes)
    apply_patch(wave4_weapon)

    for target in [ROOT / 'src' / 'BenjaminMenu' / 'Gameplay' / 'WeaponCombatModule.cs', ROOT / 'src' / 'BenjaminMenu' / 'BenjaminMenu.csproj']:
        target.write_bytes(target.read_bytes().replace(b'\r\n', b'\n'))
    apply_patch(lf_copy(TOOLS / 'wave4-compilefix.patch', TOOLS / 'wave4-compilefix.lf.patch'))
    apply_patch(lf_copy(TOOLS / 'wave4-compilefix2.patch', TOOLS / 'wave4-compilefix2.lf.patch'))

    normalize_text_sources()
    apply_patch(reconstruct_parts('wave4grenadefixparts', 2, 'wave4-grenadefix.patch', '7c95a7d6b793bcfeb106394a1c67ac119a898f53c6b47e64aa1852cbf30f7f44', 'fea9a5f20b861d6c95a1969f70c77b257b0430b55153a14bbca7ecdba0ca3f6f'))

    normalize_text_sources()
    wave4b = TOOLS / 'wave4b-silentaim.patch'
    write_decoded_b64_gzip(TOOLS / 'wave4b-silentaim.patch.gz.b64', wave4b, '2c4db15655c1ec00962de9d27144f6a51ba385ccb28b5836759cb9f09df2f2cb')
    apply_patch(wave4b)
    print('Wave 4B source integration completed.', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

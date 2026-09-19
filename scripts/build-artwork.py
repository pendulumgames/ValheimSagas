"""Build three responsive delivery sizes from each original artwork source.

Requires Pillow12.3.0 (isolated venv recommended). No AI service/network calls.
Upscaling must be explicit and is recorded; it does not create native detail.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageOps, __version__ as pillow_version

ROOT = Path(__file__).resolve().parents[1]
BIOMES = ('meadows', 'black-forest', 'swamp', 'mountain', 'plains', 'mistlands', 'ashlands', 'deep-north')
SETS = ('grounded',)
SIZES = ((1920, 1080), (2560, 1440), (3840, 2160))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-version', default='v034', choices=('v032','v034'), help='Versioned original artwork directory.')
    parser.add_argument('--allow-upscale', action='store_true', help='Allow resized delivery copies above native source detail; record this in the report.')
    args = parser.parse_args()
    sources = ROOT / 'assets' / 'artwork' / args.source_version
    output = ROOT / 'src' / 'Sagas.Web' / 'biomes'
    # Validate every source before producing any deliverable.
    for theme in SETS:
        for biome in BIOMES:
            path = sources / theme / (biome + '.png')
            with Image.open(path) as image:
                image.verify()
            with Image.open(path) as image:
                if not args.allow_upscale and (image.width < 3840 or image.height < 2160):
                    raise ValueError(f'{path}: {image.size} is below3840x2160. Supply a native larger source or explicitly --allow-upscale.')
    report = {'pillow': pillow_version, 'format': 'WebP', 'quality': 92, 'method': 6,
              'resampling': 'Lanczos; centered16:9 fit', 'native_4k_claim': False, 'images': []}
    for theme in SETS:
        (output / theme).mkdir(parents=True, exist_ok=True)
        for biome in BIOMES:
            source = sources / theme / (biome + '.png')
            with Image.open(source) as original:
                original.load()
                image = original.convert('RGB')
                item = {'set': theme, 'biome': biome, 'source': str(source.relative_to(ROOT)).replace('\\', '/'),
                        'source_size': [image.width, image.height],
                        'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'variants': []}
                for width, height in SIZES:
                    target = output / theme / f'{biome}-{width}.webp'
                    resized = ImageOps.fit(image, (width, height), method=Image.Resampling.LANCZOS, centering=(0.5, 0.5))
                    resized.save(target, 'WEBP', quality=92, method=6)
                    data = target.read_bytes()
                    with Image.open(target) as check:
                        check.load()
                        if check.size != (width, height) or check.format != 'WEBP':
                            raise ValueError(f'Unexpected output: {target}')
                    item['variants'].append({'path': str(target.relative_to(ROOT)).replace('\\', '/'),
                                             'size': [width, height], 'bytes': len(data),
                                             'upscaled': width > image.width or height > image.height,
                                             'sha256': hashlib.sha256(data).hexdigest()})
                report['images'].append(item)
    (sources / 'delivery-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    variants = [v for i in report['images'] for v in i['variants']]
    print(f'Built {len(variants)} verified WebP delivery files; {sum(v["bytes"] for v in variants):,} bytes.')
    print(f'{sum(v["upscaled"] for v in variants)} resized above source detail. See assets/artwork/{args.source_version}/delivery-report.json.')


if __name__ == '__main__':
    main()

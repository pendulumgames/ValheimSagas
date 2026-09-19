from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parent.parent
scale = 4
image = Image.new('RGB', (256 * scale, 256 * scale), '#101c22')
draw = ImageDraw.Draw(image)
# Original Sagas rune from our favicon; no game assets.
points = [(43*16,11*16),(22*16,30*16),(42*16,39*16),(21*16,53*16)]
draw.line(points, fill='#d4b374', width=5*16, joint='curve')
image.resize((256,256), Image.Resampling.LANCZOS).save(root / 'icon.png', optimize=True)
print('Created original 256 x 256 package icon')

// Turns a chosen image File into a small, square, self-contained data URL — center-cropped
// and downscaled on a canvas so the stored avatar stays tiny (deployment-safe: no blob store,
// the image travels inside the JSON). Returns a JPEG data URL.

const SIZE = 256; // output edge in px

export async function fileToAvatarDataUrl(file: File): Promise<string> {
  if (!file.type.startsWith('image/')) {
    throw new Error('Please choose an image file.');
  }

  const bitmap = await loadImage(file);
  const canvas = document.createElement('canvas');
  canvas.width = SIZE;
  canvas.height = SIZE;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Could not process the image.');

  // Center-crop to a square, then scale to SIZE×SIZE.
  const edge = Math.min(bitmap.width, bitmap.height);
  const sx = (bitmap.width - edge) / 2;
  const sy = (bitmap.height - edge) / 2;
  ctx.drawImage(bitmap, sx, sy, edge, edge, 0, 0, SIZE, SIZE);

  return canvas.toDataURL('image/jpeg', 0.85);
}

function loadImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(img);
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error('That image could not be loaded.'));
    };
    img.src = url;
  });
}

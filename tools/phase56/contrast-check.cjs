function luminance(hex) {
  const value = hex.replace('#', '');

  const rgb = [0, 2, 4]
    .map((index) => parseInt(value.slice(index, index + 2), 16) / 255)
    .map((channel) => {
      if (channel <= 0.03928) return channel / 12.92;
      return Math.pow((channel + 0.055) / 1.055, 2.4);
    });

  return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
}

function contrastRatio(foreground, background) {
  const first = luminance(foreground);
  const second = luminance(background);
  const high = Math.max(first, second);
  const low = Math.min(first, second);
  return (high + 0.05) / (low + 0.05);
}

const pairs = [
  ['dark text on navy', '#EAF6FF', '#050B18', 4.5],
  ['dark muted on navy', '#B7D4E8', '#050B18', 4.5],
  ['light text on light bg', '#0B1F33', '#F4F9FC', 4.5],
  ['light muted on light bg', '#315B73', '#F4F9FC', 4.5],
  ['focus on navy', '#FFBF47', '#050B18', 3]
];

let failed = false;

for (const [label, foreground, background, minimum] of pairs) {
  const ratio = contrastRatio(foreground, background);
  console.log(`${label}: ${ratio.toFixed(2)}:1`);

  if (ratio < minimum) {
    failed = true;
    console.error(`FAIL ${label}: expected >= ${minimum}`);
  }
}

if (failed) {
  process.exit(1);
}

console.log('Phase 6 contrast gate passed.');
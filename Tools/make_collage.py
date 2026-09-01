# -*- coding: utf-8 -*-
"""Workshop/screenshots/collage.jpg — 오락 창들을 한 판에 모은 콜라주 (설명문 SS1 용).

각 스크린샷(2560x1440)에서 게임 창만 오려 격자로 앉힌다.
편자 던지기 창이 아직 없어서 지금은 여섯 칸이다 — 스크린샷이 오면
CROPS 에 한 줄 넣고 다시 돌리면 된다.
"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHOTS = os.path.join(ROOT, 'Workshop', 'screenshots')
BG = (0x16, 0x13, 0x0F)

# 파일명 → 창 영역 (left, top, right, bottom) — 2560x1440 원본 기준
CROPS = [
    ('chess.jpg',            (529, 151, 2031, 1297)),
    ('nineball.jpg',         (529, 183, 2031, 1261)),
    ('poker.jpg',            (516, 137, 2044, 1303)),
    ('ur.jpg',               (547, 183, 2013, 1261)),
    ('hoopstone.jpg',        (636, 195, 1924, 1244)),
    ('stargazing_night.jpg', (397, 124, 2163, 1318)),
]

COLS = 3
CELL_W, CELL_H = 840, 620
GAP = 14


def main():
    rows = (len(CROPS) + COLS - 1) // COLS
    W = COLS * CELL_W + (COLS + 1) * GAP
    H = rows * CELL_H + (rows + 1) * GAP
    sheet = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(sheet)

    for i, (name, box) in enumerate(CROPS):
        img = Image.open(os.path.join(SHOTS, name)).crop(box)
        scale = min(CELL_W / img.width, CELL_H / img.height)
        img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)

        col, row = i % COLS, i // COLS
        x0 = GAP + col * (CELL_W + GAP)
        y0 = GAP + row * (CELL_H + GAP)
        x = x0 + (CELL_W - img.width) // 2
        y = y0 + (CELL_H - img.height) // 2
        sheet.paste(img, (x, y))
        d.rectangle([x - 1, y - 1, x + img.width, y + img.height],
                    outline=(0x5C, 0x4C, 0x35), width=2)

    out = os.path.join(SHOTS, 'collage.jpg')
    sheet.resize((1920, int(H * 1920 / W)), Image.LANCZOS).save(out, quality=90)
    print('collage.jpg  %dx%d  %.0f KB' % (1920, int(H * 1920 / W),
                                           os.path.getsize(out) / 1024.0))


main()

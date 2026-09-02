# -*- coding: utf-8 -*-
"""Workshop/screenshots/collage.jpg — 오락 창들을 한 판에 모은 콜라주 (설명문 SS1 용).

각 스크린샷(2560x1440)에서 게임 창만 오려 격자로 앉힌다.
윗줄이 판·패 게임 넷, 아랫줄이 던지기 둘과 하늘 둘이다.
"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHOTS = os.path.join(ROOT, 'Workshop', 'screenshots')
BG = (0x16, 0x13, 0x0F)

# 파일명 → 창 영역 (left, top, right, bottom) — 2560x1440 원본 기준
# 창은 화면 중앙 고정이라 같은 게임이면 좌표를 그대로 다시 쓴다.
CROPS = [
    ('chess.jpg',             (529, 151, 2031, 1297)),
    ('ur.jpg',                (547, 183, 2013, 1261)),
    ('poker.jpg',             (516, 137, 2044, 1303)),
    ('nineball.jpg',          (529, 183, 2031, 1261)),
    ('horseshoes.jpg',        (636, 195, 1924, 1244)),
    ('hoopstone.jpg',         (636, 195, 1924, 1244)),
    ('stargazing_naming.jpg', (397, 124, 2163, 1318)),
    ('orbit_sky.jpg',         (397, 124, 2163, 1318)),
]

COLS = 4
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

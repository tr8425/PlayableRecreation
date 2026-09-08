# -*- coding: utf-8 -*-
"""Workshop/screenshots/mod_collage.jpg — 다른 모드 가구에서 열리는 창들을 한 판에.

창작마당 캐러셀에 다섯 장을 따로 거는 대신 한 장으로 묶는다. 이 다섯은 우리 가구가
아니라 남의 가구에서 열리므로, 낱장으로 걸면 "이 모드를 깔아야 하나" 하는 오해만 는다.
한 판에 모아 두면 그 자체로 '있으면 늘어난다'는 말이 된다.

원본은 스팀 스크린샷(2560x1440). 게임 창만 오려 격자에 앉히고 아래에 이름을 적는다.
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHOTS = os.path.join(ROOT, 'Workshop', 'screenshots')
SOURCE = os.path.join(SHOTS, 'mod_source')

BG = (0x16, 0x13, 0x0F)
EDGE = (0x5C, 0x4C, 0x35)
INK = (0xC8, 0xBA, 0xA2)

# 파일 → (창 영역, 캡션). 창은 화면 중앙 고정이라 게임마다 크기가 다르다.
ROWS = [
    [
        ('darts.jpg',    (640, 183, 1918, 1259), 'Darts  ·  Vanilla Furniture Expanded'),
        ('roulette.jpg', (607, 198, 1953, 1240), 'Roulette  ·  Vanilla Furniture Expanded'),
        ('punching.jpg', (710, 211, 1850, 1197), 'Punching bag  ·  Vanilla Furniture Expanded'),
    ],
    [
        ('arcade.jpg',   (934, 348, 1623, 1092), 'Arcade machine  ·  Vanilla Furniture Expanded'),
        ('slots.jpg',    (815, 303, 1742, 1137), 'Slot machine  ·  Hospitality: Casino'),
    ],
]

CELL_W, CELL_H = 840, 600
LABEL_H = 42
GAP = 14


def font_for(size):
    for name in ('segoeui.ttf', 'arial.ttf', 'tahoma.ttf'):
        path = os.path.join('C:\\', 'Windows', 'Fonts', name)
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def main():
    cols = max(len(row) for row in ROWS)
    cell = CELL_H + LABEL_H

    W = cols * CELL_W + (cols + 1) * GAP
    H = len(ROWS) * cell + (len(ROWS) + 1) * GAP

    sheet = Image.new('RGB', (W, H), BG)
    draw = ImageDraw.Draw(sheet)
    font = font_for(26)

    for r, row in enumerate(ROWS):
        # 모자란 줄은 가운데로 민다 - 왼쪽에 몰아 두면 빠뜨린 자리처럼 보인다.
        span = len(row) * CELL_W + (len(row) - 1) * GAP
        left = (W - span) // 2

        for c, (name, box, caption) in enumerate(row):
            img = Image.open(os.path.join(SOURCE, name)).crop(box)
            scale = min(CELL_W / img.width, CELL_H / img.height)
            img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)

            x0 = left + c * (CELL_W + GAP)
            y0 = GAP + r * (cell + GAP)

            x = x0 + (CELL_W - img.width) // 2
            y = y0 + (CELL_H - img.height) // 2
            sheet.paste(img, (x, y))
            draw.rectangle([x - 1, y - 1, x + img.width, y + img.height], outline=EDGE, width=2)

            draw.text((x0 + CELL_W / 2, y0 + CELL_H + LABEL_H / 2), caption,
                      font=font, fill=INK, anchor='mm')

    out = os.path.join(SHOTS, 'mod_collage.jpg')
    height = int(H * 1920 / W)
    sheet.resize((1920, height), Image.LANCZOS).save(out, quality=90)
    print('mod_collage.jpg  %dx%d  %.0f KB' % (1920, height, os.path.getsize(out) / 1024.0))


main()

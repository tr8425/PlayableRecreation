# -*- coding: utf-8 -*-
"""About/Preview.png (640x360) 과 About/ModIcon.png (64x64) 생성.

바닐라 톤(역청 배경 · 조가비 상감 · 청금석 · 황토)에 맞춘 손그림 느낌의 정적 이미지.
"""
import math, os
from PIL import Image, ImageDraw, ImageFont

ROOT = r'C:/Users/tr842/orca/projects/RoyalGameOfUr'
S = 3  # supersampling

BITUMEN   = (0x16, 0x13, 0x0F)
BOARD_BG  = (0x24, 0x1F, 0x18)
CELL      = (0x33, 0x2E, 0x26)
CELL_ROS  = (0x4E, 0x3F, 0x23)
BORDER    = (0x85, 0x74, 0x59)
GOLD      = (0xC9, 0xA2, 0x27)
SHELL     = (0xED, 0xE4, 0xD3)
LAPIS     = (0x4C, 0x74, 0xC4)
OCHRE     = (0xE0, 0xB4, 0x54)
DIM       = (0x9A, 0x8D, 0x78)

ROWS, COLS = 3, 8
MISSING = {(0, 4), (0, 5), (2, 4), (2, 5)}
ROSETTES = {(0, 0), (0, 6), (1, 3), (2, 0), (2, 6)}


def font(names, size):
    for n in names:
        for d in (r'C:/Windows/Fonts/',):
            p = d + n
            if os.path.exists(p):
                try:
                    return ImageFont.truetype(p, size)
                except Exception:
                    pass
    return ImageFont.load_default()


def rosette(draw, cx, cy, r, colour, petals=8):
    pts = []
    for i in range(360):
        a = math.radians(i)
        edge = 0.52 + 0.34 * math.cos(petals * a)
        pts.append((cx + r * edge * math.cos(a), cy + r * edge * math.sin(a)))
    draw.polygon(pts, fill=colour)


def draw_board(draw, x0, y0, cell, gap, pieces):
    for row in range(ROWS):
        for col in range(COLS):
            if (row, col) in MISSING:
                continue

            x = x0 + col * (cell + gap)
            y = y0 + row * (cell + gap)
            box = [x, y, x + cell, y + cell]
            ros = (row, col) in ROSETTES

            draw.rounded_rectangle(box, radius=cell * 0.12,
                                   fill=CELL_ROS if ros else CELL,
                                   outline=BORDER, width=max(1, cell // 26))

            if ros:
                rosette(draw, x + cell / 2, y + cell / 2, cell * 0.40, GOLD + (0,)[:0] or GOLD)

            piece = pieces.get((row, col))
            if piece == 'p':
                pad = cell * 0.20
                draw.ellipse([x + pad, y + pad, x + cell - pad, y + cell - pad], fill=OCHRE)
            elif piece == 'b':
                pad = cell * 0.20
                draw.ellipse([x + pad, y + pad, x + cell - pad, y + cell - pad],
                             outline=LAPIS, width=int(cell * 0.13))


def preview():
    W, H = 640 * S, 360 * S
    img = Image.new('RGB', (W, H), BITUMEN)
    d = ImageDraw.Draw(img)

    # 은은한 상단 광원
    for i in range(H):
        t = 1.0 - i / float(H)
        v = int(10 * t * t)
        d.line([(0, i), (W, i)], fill=(BITUMEN[0] + v, BITUMEN[1] + v, BITUMEN[2] + v))

    title_f = font(['georgiab.ttf', 'timesbd.ttf', 'malgunbd.ttf'], 46 * S)
    sub_f = font(['malgun.ttf', 'georgia.ttf'], 19 * S)
    tag_f = font(['malgun.ttf', 'georgia.ttf'], 15 * S)

    d.text((44 * S, 34 * S), 'ROYAL GAME OF UR', font=title_f, fill=SHELL)
    d.text((46 * S, 88 * S), '\uc6b0\ub974\uc758 \uac8c\uc784 \u2014 \uc9c1\uc811 \ub458 \uc218 \uc788\ub294 \uc624\ub77d\uac00\uad6c',
           font=sub_f, fill=GOLD)

    d.line([(46 * S, 124 * S), (268 * S, 124 * S)], fill=(0x4A, 0x40, 0x31), width=max(1, S))

    cell, gap = 46 * S, 6 * S
    bw = COLS * (cell + gap) - gap
    bh = ROWS * (cell + gap) - gap
    x0 = (W - bw) // 2
    y0 = 154 * S

    d.rounded_rectangle([x0 - 14 * S, y0 - 14 * S, x0 + bw + 14 * S, y0 + bh + 14 * S],
                        radius=10 * S, fill=BOARD_BG, outline=(0x5C, 0x4C, 0x35), width=max(1, S))

    # 로제트 칸은 문양이 보이도록 비워 둔다.
    pieces = {(2, 2): 'p', (2, 3): 'p', (1, 2): 'p', (1, 4): 'p',
              (0, 1): 'b', (0, 2): 'b', (1, 6): 'b', (1, 7): 'b'}
    draw_board(d, x0, y0, cell, gap, pieces)

    d.text((44 * S, 322 * S),
           '4 binary dice  \u00b7  5 AI opponents  \u00b7  no Harmony patches  \u00b7  vanilla Defs untouched',
           font=tag_f, fill=DIM)

    img.resize((640, 360), Image.LANCZOS).save(ROOT + '/About/Preview.png')
    print('Preview.png')


def icon():
    N = 64 * S
    img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, N - 1, N - 1], radius=8 * S, fill=BITUMEN + (255,),
                        outline=(0x5C, 0x4C, 0x35, 255), width=2 * S)
    rosette(d, N / 2, N / 2, N * 0.40, GOLD + (255,))

    img.resize((64, 64), Image.LANCZOS).save(ROOT + '/About/ModIcon.png')
    print('ModIcon.png')


preview()
icon()

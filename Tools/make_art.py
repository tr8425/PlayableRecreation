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


def panel(d, box, label, font_):
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, radius=8 * S, fill=BOARD_BG, outline=(0x5C, 0x4C, 0x35), width=max(1, S))
    d.text((x0 + 12 * S, y1 - 24 * S), label, font=font_, fill=DIM)


def rings(d, cx, cy, radii, marks):
    """과녁 하나. radii 는 바깥에서 안쪽 순서, marks 는 (거리비, 각도, 편) 목록."""
    for i, r in enumerate(radii):
        d.ellipse([cx - r, cy - r, cx + r, cy + r],
                  outline=GOLD if i == len(radii) - 1 else (0x6A, 0x5C, 0x45),
                  width=max(1, int(S * 1.4)))

    outer = radii[0]
    for ratio, angle, side in marks:
        a = math.radians(angle)
        px = cx + outer * ratio * math.cos(a)
        py = cy + outer * ratio * math.sin(a)
        m = 5.5 * S
        if side == 'p':
            d.ellipse([px - m, py - m, px + m, py + m], fill=OCHRE)
        else:
            d.ellipse([px - m, py - m, px + m, py + m], outline=LAPIS, width=max(1, int(S * 1.8)))


def preview():
    W, H = 640 * S, 360 * S
    img = Image.new('RGB', (W, H), BITUMEN)
    d = ImageDraw.Draw(img)

    # 은은한 상단 광원
    for i in range(H):
        t = 1.0 - i / float(H)
        v = int(10 * t * t)
        d.line([(0, i), (W, i)], fill=(BITUMEN[0] + v, BITUMEN[1] + v, BITUMEN[2] + v))

    title_f = font(['georgiab.ttf', 'timesbd.ttf', 'malgunbd.ttf'], 40 * S)
    sub_f = font(['malgun.ttf', 'georgia.ttf'], 18 * S)
    tag_f = font(['malgun.ttf', 'georgia.ttf'], 14 * S)
    cap_f = font(['malgun.ttf', 'georgia.ttf'], 13 * S)

    d.text((44 * S, 30 * S), 'PLAYABLE RECREATION', font=title_f, fill=SHELL)
    d.text((46 * S, 78 * S), u'오락은 식민자에게 맡기고 당신은 구경만 했다면',
           font=sub_f, fill=GOLD)
    d.line([(46 * S, 110 * S), (300 * S, 110 * S)], fill=(0x4A, 0x40, 0x31), width=max(1, S))

    top, bottom = 132 * S, 306 * S
    gap = 14 * S
    width = (W - 88 * S - gap * 2) / 3.0
    left = 44 * S

    boxes = [(left + i * (width + gap), top, left + i * (width + gap) + width, bottom) for i in range(3)]

    # 1. 우르 - 축소한 판
    panel(d, boxes[0], u'우르의 게임', cap_f)
    cell, cgap = 19 * S, 3 * S
    bw = COLS * (cell + cgap) - cgap
    bh = ROWS * (cell + cgap) - cgap
    bx = boxes[0][0] + (width - bw) / 2
    by = top + (bottom - top - bh) / 2 - 8 * S
    draw_board(d, bx, by, cell, cgap, {(2, 2): 'p', (1, 4): 'p', (0, 2): 'b', (1, 7): 'b'})

    # 2. 편자 - 넓은 과녁, 양쪽이 흩어져 있다
    panel(d, boxes[1], u'편자 던지기', cap_f)
    cx = (boxes[1][0] + boxes[1][2]) / 2
    cy = top + (bottom - top) / 2 - 8 * S
    rings(d, cx, cy, [58 * S, 22 * S],
          [(0.90, 200, 'b'), (0.55, 40, 'p'), (0.28, 310, 'p'), (0.72, 130, 'b')])
    d.ellipse([cx - 4 * S, cy - 4 * S, cx + 4 * S, cy + 4 * S], fill=SHELL)

    # 3. 후프스톤 - 좁은 과녁, 가운데가 고리다
    panel(d, boxes[2], u'후프스톤', cap_f)
    cx = (boxes[2][0] + boxes[2][2]) / 2
    rings(d, cx, cy, [58 * S, 30 * S],
          [(0.95, 165, 'b'), (0.44, 20, 'p'), (0.12, 255, 'p')])
    d.ellipse([cx - 9 * S, cy - 9 * S, cx + 9 * S, cy + 9 * S], outline=SHELL, width=max(1, int(S * 1.8)))

    d.text((44 * S, 324 * S),
           '3 games  ·  5 AI tiers each  ·  no Harmony patches  ·  vanilla Defs untouched',
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

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


CARD_FONT = None
TAG_FONT = None


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


def chess_panel(img, d, box):
    """체스 - 8x8 과 기물 몇 개. 기물 그림은 게임에 들어가는 것과 같은 도형이다."""
    import chess_pieces

    cell = int(9 * S)
    span = cell * 8
    bx, by = art_origin(box, span, span)

    for row in range(8):
        for col in range(8):
            dark = (row + col) % 2 == 1
            d.rectangle([bx + col * cell, by + row * cell,
                         bx + (col + 1) * cell - 1, by + (row + 1) * cell - 1],
                        fill=(0x38, 0x30, 0x27) if dark else (0x8A, 0x7A, 0x5E))

    d.rectangle([bx - 1, by - 1, bx + span, by + span], outline=(0x5C, 0x4C, 0x35), width=max(1, S))

    # 게임 안에서와 같은 방식 - 실루엣을 조금 키워 테두리색으로 깔고 그 위에 제 색으로.
    placed = [('king', 4, 7, SHELL, (0x2A, 0x24, 0x1C)),
              ('knight', 2, 5, SHELL, (0x2A, 0x24, 0x1C)),
              ('queen', 3, 1, (0x18, 0x16, 0x15), (0xB4, 0xA8, 0x94)),
              ('pawn', 5, 3, (0x18, 0x16, 0x15), (0xB4, 0xA8, 0x94))]

    edge = max(2, int(S * 0.7))

    for name, col, row, colour, rim in placed:
        under = chess_pieces.render(name, cell + edge * 2, rim)
        img.paste(under, (bx + col * cell - edge, by + row * cell - edge), under)

        piece = chess_pieces.render(name, cell, colour)
        img.paste(piece, (bx + col * cell, by + row * cell), piece)


def poker_panel(img, d, box):
    """포커 - 판에 깔린 다섯 장. 무늬는 게임에 들어가는 것과 같은 알파다."""
    import card_suits

    width = int(15 * S)
    height = int(21 * S)
    gap = int(2 * S)
    span = width * 5 + gap * 4

    bx, by = art_origin(box, span, height)

    faces = [('spade', 'A'), ('heart', 'K'), ('diamond', '7'), ('club', '7'), ('spade', '2')]

    for i, (suit, rank) in enumerate(faces):
        x = bx + i * (width + gap)
        d.rounded_rectangle([x, by, x + width, by + height], radius=2 * S, fill=(0xEF, 0xEA, 0xDC))

        ink = (0xBD, 0x33, 0x2C) if suit in ('heart', 'diamond') else (0x22, 0x20, 0x22)
        pip = card_suits.render(suit, int(width * 0.52), ink)
        img.paste(pip, (int(x + width * 0.24), int(by + height * 0.38)), pip)

        d.text((x + 2 * S, by + 1 * S), rank, font=CARD_FONT, fill=ink)

    d.text((bx, by + height + 4 * S), 'POT 240', font=TAG_FONT, fill=OCHRE)


def pool_panel(d, box):
    """당구 - 천과 쿠션, 그리고 다음에 맞혀야 할 공."""
    x0, y0, x1, y1 = box
    width = (x1 - x0) - 14 * S
    height = width / 2

    tx, ty = art_origin(box, width, height)

    d.rounded_rectangle([tx - 3 * S, ty - 3 * S, tx + width + 3 * S, ty + height + 3 * S],
                        radius=4 * S, fill=(0x42, 0x2B, 0x1C))
    d.rectangle([tx, ty, tx + width, ty + height], fill=(0x22, 0x4F, 0x36))

    hole = 3.4 * S
    for fx in (0.0, 0.5, 1.0):
        for fy in (0.0, 1.0):
            cx, cy = tx + width * fx, ty + height * fy
            d.ellipse([cx - hole, cy - hole, cx + hole, cy + hole], fill=(0x0D, 0x0D, 0x0D))

    d.line([(tx + width * 0.20, ty + height * 0.52), (tx + width * 0.58, ty + height * 0.40)],
           fill=(0xC8, 0xC0, 0xB0), width=max(1, S))

    balls = [(0.20, 0.52, SHELL), (0.58, 0.40, OCHRE), (0.66, 0.62, (0xC4, 0x40, 0x38)),
             (0.80, 0.30, LAPIS), (0.88, 0.66, OCHRE)]
    r = 2.8 * S

    for fx, fy, colour in balls:
        cx, cy = tx + width * fx, ty + height * fy
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=colour)


def sky_panel(d, box):
    """별 보기 - 원판 하나에 별을 뿌리고 넷을 이어 둔다. 게임 안에서 하는 그대로다."""
    import math
    import random

    x0, y0, x1, y1 = box
    size = min((x1 - x0) - 14 * S, (y1 - y0) - 26 * S)
    cx = (x0 + x1) / 2
    cy = y0 + (y1 - y0 - 22 * S) / 2
    r = size / 2

    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(0x09, 0x0C, 0x14))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(0x4A, 0x52, 0x62), width=max(1, S))

    rng = random.Random(20260831)
    for _ in range(70):
        a = rng.random() * math.tau
        rad = r * math.sqrt(rng.random()) * 0.97
        px, py = cx + rad * math.cos(a), cy + rad * math.sin(a)
        dot = S * rng.choice([0.5, 0.5, 0.7, 1.0, 1.5])
        d.ellipse([px - dot, py - dot, px + dot, py + dot], fill=(0xE6, 0xEC, 0xFA))

    shape = [(-0.44, -0.30), (-0.16, -0.44), (0.12, -0.22), (0.30, 0.14), (0.06, 0.36)]
    points = [(cx + fx * r, cy + fy * r) for fx, fy in shape]

    d.line(points, fill=(0xE0, 0xB4, 0x54), width=max(1, S))
    for px, py in points:
        d.ellipse([px - 2.2 * S, py - 2.2 * S, px + 2.2 * S, py + 2.2 * S], fill=(0xF4, 0xDC, 0x9A))


def art_origin(box, width, height):
    """그림을 칸 안에서 가운데에, 이름표 자리를 남기고 앉힌다."""
    x0, y0, x1, y1 = box
    return (int((x0 + x1) / 2 - width / 2), int(y0 + (y1 - y0 - 22 * S - height) / 2))


def preview():
    global CARD_FONT, TAG_FONT

    W, H = 640 * S, 360 * S
    img = Image.new('RGB', (W, H), BITUMEN)
    d = ImageDraw.Draw(img)

    # 은은한 상단 광원
    for i in range(H):
        t = 1.0 - i / float(H)
        v = int(10 * t * t)
        d.line([(0, i), (W, i)], fill=(BITUMEN[0] + v, BITUMEN[1] + v, BITUMEN[2] + v))

    title_f = font(['georgiab.ttf', 'timesbd.ttf', 'malgunbd.ttf'], 34 * S)
    sub_f = font(['malgun.ttf', 'georgia.ttf'], 15 * S)
    cap_f = font(['malgun.ttf', 'georgia.ttf'], 10 * S)
    CARD_FONT = font(['georgiab.ttf', 'timesbd.ttf'], 8 * S)
    TAG_FONT = font(['malgun.ttf', 'georgia.ttf'], 8 * S)

    d.text((44 * S, 20 * S), 'PLAYABLE RECREATION', font=title_f, fill=SHELL)
    d.text((46 * S, 60 * S), u'오락은 식민자에게 맡기고 당신은 구경만 했다면',
           font=sub_f, fill=GOLD)
    d.line([(46 * S, 86 * S), (280 * S, 86 * S)], fill=(0x4A, 0x40, 0x31), width=max(1, S))

    # 네 칸씩 두 줄. 마지막 한 칸은 그림 대신 한 줄 요약이 들어간다.
    left = 44 * S
    gap = 9 * S
    width = (W - 88 * S - gap * 3) / 4.0
    height = 112 * S
    top = 100 * S
    second = top + height + gap

    def cell(index):
        row, col = divmod(index, 4)
        x = left + col * (width + gap)
        y = top if row == 0 else second
        return (x, y, x + width, y + height)

    # 1. 우르
    box = cell(0)
    panel(d, box, u'우르의 게임', cap_f)
    bw = COLS * (7 * S + 2 * S) - 2 * S
    bh = ROWS * (7 * S + 2 * S) - 2 * S
    bx, by = art_origin(box, bw, bh)
    draw_board(d, bx, by, 7 * S, 2 * S, {(2, 2): 'p', (1, 4): 'p', (0, 2): 'b', (1, 7): 'b'})

    # 2. 체스
    box = cell(1)
    panel(d, box, u'체스', cap_f)
    chess_panel(img, d, box)

    # 3. 포커
    box = cell(2)
    panel(d, box, u'포커', cap_f)
    poker_panel(img, d, box)

    # 4. 별 보기
    box = cell(3)
    panel(d, box, u'별 보기', cap_f)
    sky_panel(d, box)

    # 5. 나인볼
    box = cell(4)
    panel(d, box, u'나인볼', cap_f)
    pool_panel(d, box)

    # 6. 편자
    box = cell(5)
    panel(d, box, u'편자 던지기', cap_f)
    cx = (box[0] + box[2]) / 2
    cy = box[1] + (height - 22 * S) / 2
    rings(d, cx, cy, [36 * S, 14 * S],
          [(0.90, 200, 'b'), (0.55, 40, 'p'), (0.28, 310, 'p'), (0.72, 130, 'b')])
    d.ellipse([cx - 3 * S, cy - 3 * S, cx + 3 * S, cy + 3 * S], fill=SHELL)

    # 7. 후프스톤
    box = cell(6)
    panel(d, box, u'후프스톤', cap_f)
    cx = (box[0] + box[2]) / 2
    rings(d, cx, cy, [36 * S, 19 * S],
          [(0.95, 165, 'b'), (0.44, 20, 'p'), (0.12, 255, 'p')])
    d.ellipse([cx - 6 * S, cy - 6 * S, cx + 6 * S, cy + 6 * S], outline=SHELL, width=max(1, int(S * 1.4)))

    # 8. 그림 대신 한 줄
    box = cell(7)
    d.rounded_rectangle(box, radius=8 * S, fill=BOARD_BG, outline=(0x5C, 0x4C, 0x35), width=max(1, S))

    lines = [(u'7', title_f, GOLD, 16 * S),
             (u'가구, 전부', sub_f, SHELL, 52 * S),
             (u'AI 5단계 · 규칙 안내', cap_f, DIM, 76 * S),
             (u'Harmony 패치 없음', cap_f, DIM, 90 * S)]

    for text, face, colour, offset in lines:
        d.text((box[0] + 14 * S, box[1] + offset), text, font=face, fill=colour)

    d.text((44 * S, 340 * S),
           '7 recreations  ·  no Harmony patches  ·  vanilla Defs untouched',
           font=cap_f, fill=DIM)

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

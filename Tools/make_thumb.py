# -*- coding: utf-8 -*-
"""Workshop/Preview2.png (640x360) — 새 썸네일 후보.

우르 판을 무대로 깔고, 가운데 폰 하나, 주위에 오락 가구 일곱을 흩어 놓는다.
폰과 가구는 진짜 바닐라 텍스처다 — 먼저 Tools/extract_vanilla.py 로 뽑아 둘 것.
타이틀은 중세 사본풍(큰 머리글자 + 금박 + 장식 괘선). 기존 About/Preview.png 는
건드리지 않는다 — 바꿀 때는 이 출력물을 About/Preview.png 로 복사한다.
"""
import math, os
from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VAN = os.path.join(ROOT, 'Tools', 'vanilla')
S = 3  # supersampling

BITUMEN = (0x16, 0x13, 0x0F)
CELL    = (0x33, 0x2E, 0x26)
CELL_ROS = (0x4E, 0x3F, 0x23)
BORDER  = (0x85, 0x74, 0x59)
GOLD    = (0xC9, 0xA2, 0x27)
SHELL   = (0xED, 0xE4, 0xD3)
LAPIS   = (0x4C, 0x74, 0xC4)
OCHRE   = (0xE0, 0xB4, 0x54)

WOOD  = (133, 97, 67)          # 바닐라 WoodLog stuffProps color
SKIN  = (0xF5, 0xD8, 0xB2)
HAIR  = (0x4E, 0x36, 0x22)
CLOTH = (0x5C, 0x78, 0xB8)

ROWS, COLS = 3, 8
MISSING = {(0, 4), (0, 5), (2, 4), (2, 5)}
ROSETTES = {(0, 0), (0, 6), (1, 3), (2, 0), (2, 6)}


def font(names, size):
    for n in names:
        p = 'C:/Windows/Fonts/' + n
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, int(size))
            except Exception:
                pass
    return ImageFont.load_default()


def dim(c, f):
    return tuple(int(v * f) for v in c)


def rosette(d, cx, cy, r, colour, petals=8):
    pts = []
    for i in range(360):
        a = math.radians(i)
        edge = 0.52 + 0.34 * math.cos(petals * a)
        pts.append((cx + r * edge * math.cos(a), cy + r * edge * math.sin(a)))
    d.polygon(pts, fill=colour)


# ---------------------------------------------------------------- 바닐라 텍스처

def tex(name):
    return Image.open(os.path.join(VAN, name + '.png')).convert('RGBA')


def tint(img, colour):
    """회색조 텍스처에 재료 색을 곱한다. 게임이 스터프 색으로 하는 그대로."""
    return ImageChops.multiply(img, Image.new('RGBA', img.size, colour + (255,)))


def furn(name, width_1x, colour=WOOD, mask=None):
    """가구 하나. mask 가 있으면 마스크의 빨강 부분만 재료 색을 받는다."""
    im = tex(name)
    if mask is not None:
        im = Image.composite(tint(im, colour), im, tex(mask).split()[0])
    elif colour is not None:
        im = tint(im, colour)
    im = im.crop(im.getbbox())
    w = int(width_1x * S)
    return im.resize((w, int(im.height * w / im.width)), Image.LANCZOS)


def pawn(ph_1x):
    """정면(남쪽) 폰 — 몸 + 셔츠 + 머리 + 머리카락, 그리고 손에 카드 한 장."""
    cv = Image.new('RGBA', (128, 160), (0, 0, 0, 0))
    cv.alpha_composite(tint(tex('Naked_Male_south'), SKIN), (0, 30))
    cv.alpha_composite(tint(tex('ShirtBasic_Male_south'), CLOTH), (0, 30))
    cv.alpha_composite(tint(tex('Male_Average_Normal_south'), SKIN), (0, 1))
    cv.alpha_composite(tint(tex('Bowlcut_south'), HAIR), (0, 1))
    cv = cv.crop(cv.getbbox())

    h = int(ph_1x * S)
    cv = cv.resize((int(cv.width * h / cv.height), h), Image.LANCZOS)

    card = Image.new('RGBA', (14 * S, 18 * S), (0, 0, 0, 0))
    cd = ImageDraw.Draw(card)
    cd.rounded_rectangle([S, S, 12 * S, 16 * S], radius=1.5 * S, fill=(0xEF, 0xEA, 0xDC, 255),
                         outline=(0x17, 0x12, 0x0C, 255), width=1)
    rosette(cd, 7 * S, 8.5 * S, 4.6 * S, GOLD + (255,))
    card = card.rotate(-18, expand=True, resample=Image.BICUBIC)
    cv.alpha_composite(card, (int(cv.width * 0.60), int(h * 0.52)))
    return cv


# ---------------------------------------------------------------- 배경: 큰 우르 판

def big_board():
    """화면을 가로지르는 우르 판. 어둡게 눌러서 무대 노릇만 하게 한다."""
    cell, gap = 64 * S, 5 * S
    step = cell + gap
    w = COLS * step - gap
    h = ROWS * step - gap
    pad = 20 * S
    layer = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    d.rounded_rectangle([pad - 10 * S, pad - 10 * S, pad + w + 10 * S, pad + h + 10 * S],
                        radius=8 * S, fill=dim((0x24, 0x1F, 0x18), 0.9) + (255,))

    for row in range(ROWS):
        for col in range(COLS):
            if (row, col) in MISSING:
                continue
            x = pad + col * step
            y = pad + row * step
            ros = (row, col) in ROSETTES
            d.rounded_rectangle([x, y, x + cell, y + cell], radius=cell * 0.10,
                                fill=dim(CELL_ROS if ros else CELL, 0.62) + (255,),
                                outline=dim(BORDER, 0.55) + (255,), width=max(1, S))
            if ros:
                rosette(d, x + cell / 2, y + cell / 2, cell * 0.38, dim(GOLD, 0.60) + (255,))

    return layer.rotate(-5, expand=True, resample=Image.BICUBIC)


# ---------------------------------------------------------------- 타이틀

def gold_title(img, cy_top, segments):
    """중세 사본풍 — 머리글자는 크게, 나머지는 작게. 금박 그러데이션 + 짙은 윤곽."""
    ascent = max(f.getmetrics()[0] for _, f in segments)
    descent = max(f.getmetrics()[1] for _, f in segments)
    width = int(sum(f.getlength(t) for t, f in segments)) + 2
    mh = ascent + descent + 2

    mask = Image.new('L', (width, mh), 0)
    md = ImageDraw.Draw(mask)
    x = 0.0
    for text, f in segments:
        md.text((x, ascent), text, font=f, fill=255, anchor='ls')
        x += f.getlength(text)

    tx = (img.width - width) // 2
    ty = cy_top

    shadow = mask.point(lambda v: int(v * 0.55))
    img.paste(Image.new('RGB', mask.size, (0, 0, 0)), (tx + 3 * S, ty + 4 * S), shadow)

    edge = Image.new('RGB', mask.size, (0x28, 0x19, 0x08))
    rr = 2.3 * S
    for i in range(8):
        a = math.tau * i / 8
        img.paste(edge, (int(tx + rr * math.cos(a)), int(ty + rr * math.sin(a))), mask)

    grad = Image.new('RGB', mask.size)
    top, bottom = (0xF6, 0xE2, 0xA6), (0xA8, 0x79, 0x1C)
    gd = ImageDraw.Draw(grad)
    for row in range(mh):
        t = row / float(mh - 1)
        gd.line([(0, row), (width, row)],
                fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))
    img.paste(grad, (tx, ty), mask)
    return tx, ty, width, mh


def diamond(d, cx, cy, r, colour):
    d.polygon([(cx - r, cy), (cx, cy - r), (cx + r, cy), (cx, cy + r)], fill=colour)


def tracked(d, cx, y, text, f, fill, extra):
    total = sum(f.getlength(ch) for ch in text) + extra * (len(text) - 1)
    x = cx - total / 2
    for ch in text:
        d.text((x, y), ch, font=f, fill=fill)
        x += f.getlength(ch) + extra


# ---------------------------------------------------------------- 조립

def main():
    W, H = 640 * S, 360 * S
    img = Image.new('RGB', (W, H), BITUMEN)
    d = ImageDraw.Draw(img)

    for i in range(H):
        t = 1.0 - abs(i - H * 0.45) / (H * 0.75)
        v = int(9 * max(0.0, t) ** 2)
        d.line([(0, i), (W, i)], fill=(BITUMEN[0] + v, BITUMEN[1] + v, BITUMEN[2] + v))

    board = big_board()
    img.paste(board, (int(W / 2 - board.width / 2), int(H * 0.62 - board.height / 2)), board)

    shade = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shade)
    band = int(118 * S)
    for i in range(band):
        a = int(165 * (1.0 - i / float(band)) ** 1.4)
        sd.line([(0, i), (W, i)], fill=(0, 0, 0, a))
    for i in range(int(36 * S)):
        a = int(105 * (i / (36.0 * S)) ** 1.6)
        y = H - int(36 * S) + i
        sd.line([(0, y), (W, y)], fill=(0, 0, 0, a))
    img.paste(shade, (0, 0), shade)

    glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gx, gy = 320 * S, 212 * S
    for i in range(60, 0, -1):
        r = i * 2.4 * S
        a = int(4 + (60 - i) * 1.5)
        gd.ellipse([gx - r * 1.25, gy - r, gx + r * 1.25, gy + r], fill=(0xFF, 0xD9, 0x8C, a))
    img.paste(glow, (0, 0), glow)

    def place(spr, cx, cy, rot=0):
        if rot:
            spr = spr.rotate(rot, expand=True, resample=Image.BICUBIC)
        sh = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        hd = ImageDraw.Draw(sh)
        hw, hh = spr.width * 0.52, spr.height * 0.20
        hd.ellipse([cx * S - hw, cy * S + spr.height * 0.36 - hh,
                    cx * S + hw, cy * S + spr.height * 0.36 + hh], fill=(0, 0, 0, 105))
        img.paste(sh, (0, 0), sh)
        img.paste(spr, (int(cx * S - spr.width / 2), int(cy * S - spr.height / 2)), spr)

    def strewn(kind, cx, cy):
        """판 위에 굴러다니는 말과 주사위."""
        r = 5.2 * S
        x, y = cx * S, cy * S
        od = ImageDraw.Draw(img)
        if kind == 'p':
            od.ellipse([x - r, y - r + 1.5 * S, x + r, y + r + 1.5 * S], fill=(0, 0, 0))
            od.ellipse([x - r, y - r, x + r, y + r], fill=dim(OCHRE, 0.92))
        elif kind == 'b':
            od.ellipse([x - r, y - r, x + r, y + r], outline=dim(LAPIS, 0.92), width=int(2.2 * S))
        else:  # 사면체 주사위
            for ox, oy, ang in ((0, 0, 0), (11 * S, 4 * S, 40)):
                pts = []
                for i in range(3):
                    a = math.radians(ang + 90 + i * 120)
                    pts.append((x + ox + 6.5 * S * math.cos(a), y + oy - 6.5 * S * math.sin(a)))
                od.polygon(pts, fill=(0xDD, 0xD2, 0xBE), outline=(0x17, 0x12, 0x0C))
                tx_, ty_ = pts[0]
                od.ellipse([tx_ - 1.4 * S, ty_ + 2.5 * S, tx_ + 1.4 * S, ty_ + 5.3 * S],
                           fill=(0x2A, 0x24, 0x1C))

    place(furn('BilliardsTable_north', 92, mask='BilliardsTable_northm'), 112, 170, -7)
    place(furn('HoopstoneRing', 42), 247, 130)
    place(furn('ChessTable', 58), 502, 140, 8)
    place(furn('Telescope', 62, colour=None), 566, 222)
    place(furn('GameOfUr_south', 62), 452, 300, -6)
    place(furn('PokerTable', 76, mask='PokerTable_m'), 192, 297)
    place(furn('HorseshoesPin', 30), 66, 242)
    place(furn('Horseshoe', 16, colour=None), 90, 262, 25)
    place(furn('Horseshoe', 16, colour=None), 46, 258, -40)

    strewn('p', 398, 152)
    strewn('b', 418, 169)
    strewn('p', 431, 243)
    strewn('b', 264, 258)
    strewn('d', 348, 297)

    place(pawn(104), 320, 210)

    big = font(['palab.ttf', 'georgiab.ttf'], 47 * S)
    small = font(['palab.ttf', 'georgiab.ttf'], 37 * S)
    tag_f = font(['palabi.ttf', 'palai.ttf', 'georgiai.ttf', 'georgia.ttf'], 15 * S)
    cap_f = font(['pala.ttf', 'georgia.ttf'], 9 * S)

    tx, ty, tw, th = gold_title(img, 18 * S, [
        ('P', big), ('LAYABLE ', small), ('R', big), ('ECREATION', small)])

    od = ImageDraw.Draw(img)
    mid = ty + int(th * 0.48)
    for sx in (-1, 1):
        if sx < 0:
            x0, x1 = tx - 86 * S, tx - 16 * S
        else:
            x0, x1 = tx + tw + 16 * S, tx + tw + 86 * S
        od.line([(x0, mid), (x1, mid)], fill=dim(GOLD, 0.75), width=max(1, S))
        diamond(od, x0 if sx < 0 else x1, mid, 4.5 * S, GOLD)

    rule_y = ty + th + 5 * S
    od.line([(W / 2 - 190 * S, rule_y), (W / 2 + 190 * S, rule_y)], fill=dim(GOLD, 0.55), width=max(1, S))
    diamond(od, W / 2, rule_y, 3.5 * S, GOLD)

    tag = 'Your colonists have all the fun.  Take it back.'
    tb = od.textbbox((0, 0), tag, font=tag_f)
    od.text(((W - (tb[2] - tb[0])) / 2 + 1.5 * S, rule_y + 7 * S + 1.5 * S), tag,
            font=tag_f, fill=(0, 0, 0))
    od.text(((W - (tb[2] - tb[0])) / 2, rule_y + 7 * S), tag, font=tag_f, fill=SHELL)

    tracked(od, W / 2, 344 * S, 'SEVEN GAMES  ·  PLAY THEM YOURSELF  ·  NO DLC REQUIRED',
            cap_f, (0xB9, 0x9A, 0x55), 1.2 * S)

    out = os.path.join(ROOT, 'Workshop', 'Preview2.png')
    img.resize((640, 360), Image.LANCZOS).save(out)
    print('Preview2.png  %.0f KB' % (os.path.getsize(out) / 1024.0))


main()

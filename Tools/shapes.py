# -*- coding: utf-8 -*-
"""0~1 좌표계에 도형을 그려 알파 한 장으로 굽는다.

게임 안에서는 이 알파에 색을 입혀 쓴다 - 흑백 두 벌, 빨강·검정 두 벌을
따로 그리지 않는 이유이자, 같은 도형을 미리보기 이미지에도 그대로 쓰는 방법이다.
"""
from PIL import Image, ImageDraw

SOLID = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)
SUPERSAMPLE = 4


class Pen(object):
    """0~1 좌표를 픽셀로 옮겨 주는 얇은 껍데기."""

    def __init__(self, draw, span):
        self.d = draw
        self.span = span

    def poly(self, points, fill=SOLID):
        self.d.polygon([(x * self.span, y * self.span) for x, y in points], fill=fill)

    def box(self, x0, y0, x1, y1, fill=SOLID, radius=None):
        area = [x0 * self.span, y0 * self.span, x1 * self.span, y1 * self.span]
        if radius:
            self.d.rounded_rectangle(area, radius=radius * self.span, fill=fill)
        else:
            self.d.rectangle(area, fill=fill)

    def ell(self, x0, y0, x1, y1, fill=SOLID):
        self.d.ellipse([x0 * self.span, y0 * self.span, x1 * self.span, y1 * self.span], fill=fill)


def bake(shape, size, colour=(255, 255, 255)):
    """크게 그린 뒤 줄여서 가장자리를 부드럽게 만든다."""
    span = size * SUPERSAMPLE

    img = Image.new('RGBA', (span, span), CLEAR)
    shape(Pen(ImageDraw.Draw(img), span))
    img = img.resize((size, size), Image.LANCZOS)

    if colour == (255, 255, 255):
        return img

    tint = Image.new('RGBA', (size, size), tuple(colour) + (255,))
    tint.putalpha(img.split()[3])
    return tint

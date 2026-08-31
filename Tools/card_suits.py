# -*- coding: utf-8 -*-
"""Textures/PR/Cards/*.png 생성. 카드 무늬 네 종."""
import os
from shapes import Pen, SOLID, CLEAR, bake


def spade(p):
    p.ell(0.05, 0.32, 0.51, 0.78)
    p.ell(0.49, 0.32, 0.95, 0.78)
    p.poly([(0.06, 0.58), (0.94, 0.58), (0.50, 0.05)])
    p.poly([(0.43, 0.62), (0.57, 0.62), (0.68, 0.95), (0.32, 0.95)])


def heart(p):
    p.ell(0.05, 0.10, 0.51, 0.56)
    p.ell(0.49, 0.10, 0.95, 0.56)
    p.poly([(0.055, 0.36), (0.945, 0.36), (0.50, 0.94)])


def diamond(p):
    p.poly([(0.50, 0.03), (0.93, 0.50), (0.50, 0.97), (0.07, 0.50)])


def club(p):
    p.ell(0.34, 0.26, 0.66, 0.58)   # 세 원이 만나는 가운데를 메운다
    p.ell(0.29, 0.06, 0.71, 0.48)
    p.ell(0.05, 0.36, 0.47, 0.78)
    p.ell(0.53, 0.36, 0.95, 0.78)
    p.poly([(0.44, 0.54), (0.56, 0.54), (0.68, 0.95), (0.32, 0.95)])


SHAPES = [('club', club), ('diamond', diamond), ('heart', heart), ('spade', spade)]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Textures', 'PR', 'Cards')
SIZE = 128


def render(name, size, colour=(255, 255, 255)):
    return bake(dict(SHAPES)[name], size, colour)


if __name__ == '__main__':
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    for name, shape in SHAPES:
        bake(shape, SIZE).save(os.path.join(OUT, name + '.png'))
        print(name + '.png')

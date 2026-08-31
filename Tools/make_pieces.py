# -*- coding: utf-8 -*-
"""Textures/PR/Chess/*.png 생성. 체스판에 올라가는 기물 여섯 종."""
import os
import chess_pieces

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Textures', 'PR', 'Chess')
SIZE = 128

if not os.path.isdir(OUT):
    os.makedirs(OUT)

for name, _ in chess_pieces.SHAPES:
    chess_pieces.render(name, SIZE).save(os.path.join(OUT, name + '.png'))
    print(name + '.png')

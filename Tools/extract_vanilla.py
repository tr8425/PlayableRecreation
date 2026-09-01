# -*- coding: utf-8 -*-
"""림월드 resources.assets 에서 썸네일에 쓸 바닐라 텍스처를 뽑는다.

출력: Tools/vanilla/*.png — 루데온 저작물이므로 저장소에 커밋하지 않는다 (.gitignore).
쓰는 곳: Tools/make_thumb.py (창작마당 미리보기 합성).
필요: pip install UnityPy
"""
import os
import UnityPy

RW = r'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data'
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'vanilla')

WANT = {
    # 가구 일곱 (+ 색 마스크, 편자 소품)
    'BilliardsTable_north', 'BilliardsTable_northm',
    'ChessTable',
    'PokerTable', 'PokerTable_m',
    'GameOfUr_south',
    'HorseshoesPin', 'Horseshoe',
    'HoopstoneRing', 'HoopstoneRing_m',
    'Telescope',
    # 폰 (남쪽 = 정면)
    'Naked_Male_south',
    'Male_Average_Normal_south',
    'Bowlcut_south',
    'ShirtBasic_Male_south',
}


def main():
    os.makedirs(OUT, exist_ok=True)
    env = UnityPy.load(os.path.join(RW, 'resources.assets'))
    left = set(WANT)

    for obj in env.objects:
        if obj.type.name != 'Texture2D' or not left:
            continue
        data = obj.read()
        if data.m_Name not in left:
            continue
        img = data.image
        img.save(os.path.join(OUT, data.m_Name + '.png'))
        print('  %-28s %dx%d' % (data.m_Name, img.width, img.height))
        left.discard(data.m_Name)

    if left:
        print('!! 못 찾음:', ', '.join(sorted(left)))
    else:
        print('OK - %d 장' % len(WANT))


main()

# -*- coding: utf-8 -*-
"""GUI 전역 상태를 바꿔 놓고 되돌리지 않는 메서드를 찾는다.

Text.Anchor / Text.Font / GUI.color 는 창 하나가 아니라 그 프레임 전체가 공유한다.
한 곳에서 바꿔 놓고 안 되돌리면 그 다음에 그려지는 남의 창 글자가 틀어진다.
눈으로는 "가끔 이상해 보인다"로만 나타나므로 여기서 잡는다.

메서드 단위로 본다. 바꾸는 대입이 하나라도 있는데 기본값으로 되돌리는 대입이
하나도 없으면 알린다. 되돌리는 쪽이 있으면 통과 - 분기마다 세지는 않는다.
"""

import io
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SRC = os.path.join(ROOT, 'Source', 'PlayableRecreation')

# (무엇을, 되돌리는 값)
WATCHED = [
    ('Text.Anchor', 'TextAnchor.UpperLeft'),
    ('Text.Font', 'GameFont.Small'),
    ('GUI.color', 'Color.white'),
]

METHOD = re.compile(
    r'^[ \t]*(?:public|private|protected|internal|static|override|virtual|sealed|new|\s)+'
    r'[\w<>\[\],\.\?]+\s+(\w+)\s*\([^;{]*\)\s*$')


def methods(text):
    """{ } 를 세어 메서드 몸통을 잘라 낸다. 문자열 안의 괄호는 무시한다."""
    lines = text.split('\n')
    found = []
    i = 0

    while i < len(lines):
        match = METHOD.match(lines[i])
        if not match or lines[i].lstrip().startswith('//'):
            i += 1
            continue

        # 다음 줄이 여는 중괄호여야 메서드다.
        j = i + 1
        while j < len(lines) and not lines[j].strip():
            j += 1
        if j >= len(lines) or lines[j].strip() != '{':
            i += 1
            continue

        depth = 0
        body = []
        k = j

        while k < len(lines):
            stripped = re.sub(r'"(?:[^"\\]|\\.)*"', '""', lines[k])
            depth += stripped.count('{') - stripped.count('}')
            body.append(lines[k])
            if depth == 0:
                break
            k += 1

        found.append((match.group(1), i + 1, '\n'.join(body)))
        i = k + 1

    return found


def main():
    problems = []

    for base, _, names in os.walk(SRC):
        for name in names:
            if not name.endswith('.cs'):
                continue

            path = os.path.join(base, name)
            text = io.open(path, encoding='utf-8').read()
            shown = os.path.relpath(path, ROOT).replace('\\', '/')

            for method, line, body in methods(text):
                for what, restore in WATCHED:
                    sets = re.findall(re.escape(what) + r'\s*=\s*([^;]+);', body)
                    if not sets:
                        continue

                    # 되돌리는 대입이 하나라도 있으면 통과.
                    if any(restore in value for value in sets):
                        continue

                    problems.append('%s:%d  %s() sets %s and never restores it'
                                    % (shown, line, method, what))

    if problems:
        print('!! %d problems' % len(problems))
        for problem in problems:
            print('  - ' + problem)
        return 1

    print('OK - every method that changes GUI state puts it back')
    return 0


if __name__ == '__main__':
    sys.exit(main())

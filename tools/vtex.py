import struct, sys
def info(path):
    b = open(path,'rb').read()
    size, hv, ver, boff, bcount = struct.unpack_from('<IHHII', b, 0)
    pos = 8 + boff
    blocks = {}
    for i in range(bcount):
        t = b[pos:pos+4].decode(); off, sz = struct.unpack_from('<II', b, pos+4)
        blocks[t] = (pos+4+off, sz); pos += 12
    d, dsz = blocks['DATA']
    ver, flags = struct.unpack_from('<HH', b, d)
    w, h, depth, fmt, mips = struct.unpack_from('<HHHBB', b, d+20)
    pic, exoff, excount = struct.unpack_from('<III', b, d+28)
    extras = []
    ep = d + 32 + exoff
    for i in range(excount):
        t, o, s = struct.unpack_from('<III', b, ep)
        extras.append((t, ep+4+o, s)); ep += 12
    return dict(len=len(b), blocks=blocks, w=w, h=h, depth=depth, fmt=fmt, mips=mips, flags=flags, extras=extras, data_end=d+dsz)
if __name__ == '__main__':
    print(info(sys.argv[1]))

import lz4.block, io
from PIL import Image

def dds_header(w, h, fourcc, linsize):
    hdr = struct.pack('<4sIIIIIII44x', b'DDS ', 124, 0x1|0x2|0x4|0x1000|0x80000, h, w, linsize, 0, 1)
    pf = struct.pack('<II4sIIIII', 32, 0x4, fourcc, 0, 0, 0, 0, 0)
    caps = struct.pack('<IIII4x', 0x1000, 0, 0, 0)
    return hdr + pf + caps

BLOCK = {1: (8, b'DXT1'), 2: (16, b'DXT5'), 20: (16, b'DX10')}

def dx10(dxgi):
    return struct.pack('<IIIII', dxgi, 3, 0, 1, 0)

def decode(path, want=None):
    """Returns a PIL image of the largest mip (or the first mip whose width <= want)."""
    i = info(path); b = open(path, 'rb').read()
    sizes = None
    for t, o, s in i['extras']:
        if t == 4:
            a, mo, n = struct.unpack_from('<III', b, o)
            sizes = struct.unpack_from('<%di' % n, b, o + 4 + mo)
    fmt = i['fmt']
    if fmt not in BLOCK: raise ValueError('format %d' % fmt)
    bs, cc = BLOCK[fmt]
    def raw(level):
        w = max(1, i['w'] >> level); h = max(1, i['h'] >> level)
        return w, h, max(1, (w + 3) // 4) * max(1, (h + 3) // 4) * bs
    # The file holds mips smallest first.
    pos = i['data_end']; mips = {}
    for level in reversed(range(i['mips'])):
        w, h, n = raw(level)
        if sizes:
            cs = sizes[level]
            chunk = b[pos:pos + cs]; pos += cs
            data = chunk if cs >= n else lz4.block.decompress(chunk, uncompressed_size=n)
        else:
            data = b[pos:pos + n]; pos += n
        mips[level] = (w, h, data)
    level = 0
    if want:
        while level < i['mips'] - 1 and mips[level][0] > want: level += 1
    w, h, data = mips[level]
    head = dds_header(w, h, cc, len(data)) + (dx10(98) if cc == b'DX10' else b'')
    return Image.open(io.BytesIO(head + data)).convert('RGB')

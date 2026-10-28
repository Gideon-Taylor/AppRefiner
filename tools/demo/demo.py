"""Scripted AppRefiner demo: types into App Designer and draws explanation bubbles over it.

Usage:
    python demo.py [--scenes 1,2] [--speed 1.0] [--no-overlay] [--no-intro] [--seed N] [--out DIR]

1. Open an EMPTY PeopleCode editor in App Designer (nothing is ever saved).
2. Start the Snipping Tool recording.
3. Run this script, then click into the empty editor. The demo starts 2 seconds later.

Hold F12 to stop at any time. The script also stops if App Designer stops being the foreground
window, so it can never type into another program.
"""
from __future__ import annotations

import argparse
import csv
import ctypes
import math
import queue
import random
import sys
import threading
import time
from ctypes import wintypes
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

user32 = ctypes.WinDLL("user32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

try:
    ctypes.WinDLL("shcore").SetProcessDpiAwareness(2)
except OSError:
    user32.SetProcessDPIAware()

LRESULT = ctypes.c_ssize_t
user32.SendMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
user32.SendMessageW.restype = LRESULT
user32.GetForegroundWindow.restype = wintypes.HWND
user32.GetAncestor.restype = wintypes.HWND
user32.GetAncestor.argtypes = [wintypes.HWND, wintypes.UINT]
user32.GetWindow.restype = wintypes.HWND
user32.GetWindow.argtypes = [wintypes.HWND, wintypes.UINT]
GW_OWNER = 4

# --- Scintilla messages ------------------------------------------------------------------------
SCI_CLEARALL = 2004
SCI_GETLENGTH = 2006
SCI_GETCHARAT = 2007
SCI_GETCURRENTPOS = 2008
SCI_GOTOPOS = 2025
SCI_AUTOCCANCEL = 2101
SCI_AUTOCACTIVE = 2102
SCI_SETLINEINDENTATION = 2126
SCI_GETLINEINDENTPOSITION = 2128
SCI_GETLINEENDPOSITION = 2136
SCI_GETLINECOUNT = 2154
SCI_POINTXFROMPOSITION = 2164
SCI_POINTYFROMPOSITION = 2165
SCI_LINEFROMPOSITION = 2166
SCI_POSITIONFROMLINE = 2167
SCI_SCROLLCARET = 2169
SCI_CALLTIPCANCEL = 2201
SCI_CALLTIPACTIVE = 2202
SCI_TEXTHEIGHT = 2279
SCI_GETMARGINWIDTHN = 2243
SCI_GETMARGINS = 2253

# --- SendInput -----------------------------------------------------------------------------------
INPUT_MOUSE, INPUT_KEYBOARD = 0, 1
KEYEVENTF_KEYUP, KEYEVENTF_UNICODE = 0x0002, 0x0004
MOUSEEVENTF_MOVE, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP = 0x0001, 0x0002, 0x0004
MOUSEEVENTF_ABSOLUTE, MOUSEEVENTF_VIRTUALDESK = 0x8000, 0x4000
VK_TAB, VK_RETURN, VK_SHIFT, VK_CONTROL, VK_MENU, VK_ESCAPE, VK_SPACE = 0x09, 0x0D, 0x10, 0x11, 0x12, 0x1B, 0x20
VK_DOWN, VK_F12, VK_OEM_PERIOD = 0x28, 0x7B, 0xBE
ULONG_PTR = ctypes.c_size_t


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG), ("mouseData", wintypes.DWORD),
                ("dwFlags", wintypes.DWORD), ("time", wintypes.DWORD), ("dwExtraInfo", ULONG_PTR)]


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", wintypes.WORD), ("wScan", wintypes.WORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", ULONG_PTR)]


class HARDWAREINPUT(ctypes.Structure):
    _fields_ = [("uMsg", wintypes.DWORD), ("wParamL", wintypes.WORD), ("wParamH", wintypes.WORD)]


class _INPUTUNION(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT), ("hi", HARDWAREINPUT)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("u", _INPUTUNION)]


user32.SendInput.argtypes = [wintypes.UINT, ctypes.POINTER(INPUT), ctypes.c_int]


def _send(*inputs: INPUT) -> None:
    arr = (INPUT * len(inputs))(*inputs)
    user32.SendInput(len(inputs), arr, ctypes.sizeof(INPUT))


def _key(vk: int = 0, scan: int = 0, flags: int = 0) -> INPUT:
    i = INPUT(type=INPUT_KEYBOARD)
    i.u.ki = KEYBDINPUT(wVk=vk, wScan=scan, dwFlags=flags)
    return i


def _mouse(x: int = 0, y: int = 0, flags: int = 0) -> INPUT:
    i = INPUT(type=INPUT_MOUSE)
    i.u.mi = MOUSEINPUT(dx=x, dy=y, dwFlags=flags)
    return i


class GUITHREADINFO(ctypes.Structure):
    _fields_ = [("cbSize", wintypes.DWORD), ("flags", wintypes.DWORD), ("hwndActive", wintypes.HWND),
                ("hwndFocus", wintypes.HWND), ("hwndCapture", wintypes.HWND), ("hwndMenuOwner", wintypes.HWND),
                ("hwndMoveSize", wintypes.HWND), ("hwndCaret", wintypes.HWND), ("rcCaret", wintypes.RECT)]


class Aborted(Exception):
    pass


def window_pid(hwnd) -> int:
    pid = wintypes.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    return pid.value


def class_name(hwnd) -> str:
    buf = ctypes.create_unicode_buffer(256)
    user32.GetClassNameW(hwnd, buf, 256)
    return buf.value


def focused_window():
    fg = user32.GetForegroundWindow()
    if not fg:
        return None, None
    tid = user32.GetWindowThreadProcessId(fg, None)
    info = GUITHREADINFO(cbSize=ctypes.sizeof(GUITHREADINFO))
    if not user32.GetGUIThreadInfo(tid, ctypes.byref(info)):
        return fg, None
    return fg, info.hwndFocus


# ================================================================================================
# Overlay: a click-through, never-focused, per-pixel-alpha window over App Designer
# ================================================================================================

WS_POPUP = 0x80000000
WS_EX_LAYERED, WS_EX_TRANSPARENT, WS_EX_TOPMOST = 0x80000, 0x20, 0x8
WS_EX_TOOLWINDOW, WS_EX_NOACTIVATE = 0x80, 0x08000000
ULW_ALPHA, AC_SRC_ALPHA = 0x2, 0x1
SW_SHOWNOACTIVATE = 4
PM_REMOVE = 1

WNDPROC = ctypes.WINFUNCTYPE(LRESULT, wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM)
user32.DefWindowProcW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
user32.DefWindowProcW.restype = LRESULT
user32.CreateWindowExW.restype = wintypes.HWND
user32.CreateWindowExW.argtypes = [wintypes.DWORD, wintypes.LPCWSTR, wintypes.LPCWSTR, wintypes.DWORD,
                                   ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
                                   wintypes.HWND, wintypes.HMENU, wintypes.HINSTANCE, wintypes.LPVOID]
gdi32.CreateDIBSection.restype = wintypes.HBITMAP
gdi32.CreateDIBSection.argtypes = [wintypes.HDC, ctypes.c_void_p, wintypes.UINT,
                                   ctypes.POINTER(ctypes.c_void_p), wintypes.HANDLE, wintypes.DWORD]
gdi32.CreateCompatibleDC.restype = wintypes.HDC
gdi32.CreateCompatibleDC.argtypes = [wintypes.HDC]
user32.GetDC.argtypes = [wintypes.HWND]
user32.UpdateLayeredWindow.argtypes = [wintypes.HWND, wintypes.HDC, ctypes.c_void_p, ctypes.c_void_p,
                                       wintypes.HDC, ctypes.c_void_p, wintypes.COLORREF, ctypes.c_void_p,
                                       wintypes.DWORD]
user32.ShowWindow.argtypes = [wintypes.HWND, ctypes.c_int]
user32.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
user32.GetClientRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
user32.ClientToScreen.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.POINT)]
user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
user32.GetClassNameW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
gdi32.SelectObject.restype = wintypes.HGDIOBJ
gdi32.SelectObject.argtypes = [wintypes.HDC, wintypes.HGDIOBJ]
gdi32.DeleteObject.argtypes = [wintypes.HGDIOBJ]
gdi32.DeleteDC.argtypes = [wintypes.HDC]
user32.GetDC.restype = wintypes.HDC
user32.ReleaseDC.argtypes = [wintypes.HWND, wintypes.HDC]


class WNDCLASSW(ctypes.Structure):
    _fields_ = [("style", wintypes.UINT), ("lpfnWndProc", WNDPROC), ("cbClsExtra", ctypes.c_int),
                ("cbWndExtra", ctypes.c_int), ("hInstance", wintypes.HINSTANCE), ("hIcon", wintypes.HICON),
                ("hCursor", wintypes.HANDLE), ("hbrBackground", wintypes.HBRUSH),
                ("lpszMenuName", wintypes.LPCWSTR), ("lpszClassName", wintypes.LPCWSTR)]


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [("biSize", wintypes.DWORD), ("biWidth", wintypes.LONG), ("biHeight", wintypes.LONG),
                ("biPlanes", wintypes.WORD), ("biBitCount", wintypes.WORD), ("biCompression", wintypes.DWORD),
                ("biSizeImage", wintypes.DWORD), ("biXPelsPerMeter", wintypes.LONG),
                ("biYPelsPerMeter", wintypes.LONG), ("biClrUsed", wintypes.DWORD), ("biClrImportant", wintypes.DWORD)]


class BLENDFUNCTION(ctypes.Structure):
    _fields_ = [("BlendOp", ctypes.c_ubyte), ("BlendFlags", ctypes.c_ubyte),
                ("SourceConstantAlpha", ctypes.c_ubyte), ("AlphaFormat", ctypes.c_ubyte)]


def _font(size: int, bold: bool = False):
    for name in (("segoeuib.ttf" if bold else "segoeui.ttf"), "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


# The editor's last few pixels on the right are the separator before the minimap; the veil
# stops short of them. Raise this if the separator still fades.
VEIL_RIGHT_INSET = 4

# Palette: dark slate bubble, warm accent, white text
BUBBLE_FILL = (24, 32, 46, 238)
ACCENT = (255, 176, 32, 255)
TEXT = (255, 255, 255, 255)
SUBTEXT = (200, 210, 225, 255)
HILITE = (255, 176, 32, 255)


def _wrap(draw, text, font, width):
    lines = []
    for para in text.split("\n"):
        words, cur = para.split(" "), ""
        for w in words:
            trial = (cur + " " + w).strip()
            if draw.textlength(trial, font=font) <= width or not cur:
                cur = trial
            else:
                lines.append(cur)
                cur = w
        lines.append(cur)
    return lines


def render_bubble(text: str, title: str | None, pointer: str | None, scale: float = 1.0):
    """Draws one bubble. Returns (image, (tipx, tipy)) where tip is the pointer's point within the
    image, or the image centre-bottom for a pointer-less caption."""
    s = scale
    body_font, title_font = _font(int(19 * s)), _font(int(19 * s), bold=True)
    pad, radius, ptr, maxw, shadow = int(16 * s), int(12 * s), int(14 * s), int(460 * s), int(10 * s)
    probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    body = _wrap(probe, text, body_font, maxw)
    lh = int(body_font.size * 1.35)
    tl = _wrap(probe, title, title_font, maxw) if title else []
    textw = max([probe.textlength(l, font=body_font) for l in body] +
                [probe.textlength(l, font=title_font) for l in tl] + [40])
    w = int(textw) + 2 * pad + int(6 * s)
    h = lh * (len(body) + len(tl)) + 2 * pad + (int(4 * s) if tl else 0)
    top = ptr if pointer == "up" else 0
    W, H = w + 2 * shadow, h + ptr + 2 * shadow
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # soft shadow
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([shadow, shadow + top + 3, shadow + w, shadow + top + h + 3],
                                         radius, fill=(0, 0, 0, 110))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(shadow / 2)))

    d = ImageDraw.Draw(img)
    x0, y0 = shadow, shadow + top
    d.rounded_rectangle([x0, y0, x0 + w, y0 + h], radius, fill=BUBBLE_FILL)
    # accent bar down the left edge, clipped to the bubble's rounded corners
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([x0, y0, x0 + w, y0 + h], radius, fill=255)
    bar = Image.new("L", (W, H), 0)
    ImageDraw.Draw(bar).rectangle([x0, y0, x0 + int(6 * s), y0 + h], fill=255)
    img.paste(ACCENT, (0, 0), Image.composite(bar, Image.new("L", (W, H), 0), mask))
    cx = x0 + w // 2
    if pointer == "down":
        d.polygon([(cx - ptr, y0 + h - 1), (cx + ptr, y0 + h - 1), (cx, y0 + h + ptr)], fill=BUBBLE_FILL)
        tip = (cx, y0 + h + ptr)
    elif pointer == "up":
        d.polygon([(cx - ptr, y0 + 1), (cx + ptr, y0 + 1), (cx, y0 - ptr)], fill=BUBBLE_FILL)
        tip = (cx, y0 - ptr)
    else:
        tip = (cx, y0 + h)
    ty = y0 + pad
    for l in tl:
        d.text((x0 + pad + int(6 * s), ty), l, font=title_font, fill=ACCENT)
        ty += lh
    if tl:
        ty += int(4 * s)
    for l in body:
        d.text((x0 + pad + int(6 * s), ty), l, font=body_font, fill=TEXT)
        ty += lh
    return img, tip


class Overlay:
    """Runs its own thread with a message loop (an unpumped top-level window gets 'ghosted')."""

    def __init__(self, enabled: bool):
        self.enabled = enabled
        self.q: queue.Queue = queue.Queue()
        self.items: dict[str, dict] = {}
        self.rect = (0, 0, 1, 1)
        self.ready = threading.Event()
        if enabled:
            threading.Thread(target=self._run, daemon=True).start()
            self.ready.wait(5)

    # ---- public API (any thread) ----
    def place(self, rect):
        self.q.put(("rect", rect))

    def show(self, key, item):
        self.q.put(("show", key, item))

    def hide(self, key, fade=None):
        self.q.put(("hide", key, fade))

    def clear(self):
        self.q.put(("clear",))

    def update(self, key, fields):
        """Changes a shown item in place, with no fade."""
        self.q.put(("update", key, fields))

    # ---- overlay thread ----
    def _run(self):
        try:
            self._loop()
        except Exception:
            import traceback
            traceback.print_exc()
            print("Overlay failed; the demo carries on without bubbles.")
            self.ready.set()

    def _loop(self):
        hinst = kernel32.GetModuleHandleW(None)
        self._proc = WNDPROC(lambda h, m, w, l: user32.DefWindowProcW(h, m, w, l))
        wc = WNDCLASSW(lpfnWndProc=self._proc, hInstance=hinst, lpszClassName="ARDemoOverlay")
        user32.RegisterClassW(ctypes.byref(wc))
        ex = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
        self.hwnd = user32.CreateWindowExW(ex, "ARDemoOverlay", "AppRefiner demo overlay", WS_POPUP,
                                           0, 0, 1, 1, None, None, hinst, None)
        user32.ShowWindow(self.hwnd, SW_SHOWNOACTIVATE)
        self.ready.set()
        msg = wintypes.MSG()
        dirty = True
        while True:
            while user32.PeekMessageW(ctypes.byref(msg), None, 0, 0, PM_REMOVE):
                user32.TranslateMessage(ctypes.byref(msg))
                user32.DispatchMessageW(ctypes.byref(msg))
            while True:
                try:
                    cmd = self.q.get_nowait()
                except queue.Empty:
                    break
                dirty = True
                now = time.perf_counter()
                if cmd[0] == "rect":
                    self.rect = cmd[1]
                elif cmd[0] == "show":
                    item = dict(cmd[2], born=now, dying=None)
                    self.items[cmd[1]] = item
                elif cmd[0] == "hide" and cmd[1] in self.items:
                    self.items[cmd[1]]["dying"] = now
                    if cmd[2] is not None:
                        self.items[cmd[1]]["fade"] = cmd[2]
                elif cmd[0] == "update" and cmd[1] in self.items:
                    self.items[cmd[1]].update(cmd[2])
                elif cmd[0] == "clear":
                    for it in self.items.values():
                        it["dying"] = it["dying"] or now
            now = time.perf_counter()
            animating = False
            for k in list(self.items):
                it = self.items[k]
                fade = it.get("fade", 0.25)
                if it["dying"] is not None and now - it["dying"] > fade:
                    del self.items[k]
                    dirty = True
                elif now - it["born"] < fade or it["dying"] is not None:
                    animating = True
            if dirty or animating:
                self._paint(now)
                dirty = False
            time.sleep(1 / 60)

    def _paint(self, now):
        x, y, r, b = self.rect
        w, h = max(1, r - x), max(1, b - y)
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(canvas)
        # veils first, so bubbles (like the next scene's title) can sit on top of one
        for it in sorted(self.items.values(), key=lambda i: not i.get("veil")):
            fade = max(it.get("fade", 0.25), 1e-3)
            a = min(1.0, (now - it["born"]) / fade)
            if it["dying"] is not None:
                a = min(a, max(0.0, 1 - (now - it["dying"]) / fade))
            a = a * a * (3 - 2 * a)  # smoothstep
            if it.get("veil"):
                vx0, vy0, vx1, vy1 = [v - o for v, o in zip(it["veil"], (x, y, x, y))]
                draw.rectangle([vx0, vy0, vx1, vy1], fill=(255, 255, 255, int(255 * a)))
                continue
            for rect in it.get("hilites") or ([it["hilite"]] if it.get("hilite") else []):
                hx0, hy0, hx1, hy1 = [v - o for v, o in zip(rect, (x, y, x, y))]
                draw.rounded_rectangle([hx0 - 4, hy0 - 2, hx1 + 4, hy1 + 2], 6,
                                       outline=HILITE[:3] + (int(255 * a),), width=3)
            if it.get("img") is not None:
                img = it["img"]
                if a < 1:
                    img = img.copy()
                    img.putalpha(it["img"].getchannel("A").point(lambda v: int(v * a)))
                bx, by = it["pos"][0] - x, it["pos"][1] - y + int((1 - a) * 8)
                canvas.alpha_composite(img, (max(0, bx), max(0, by)))
        self._update(canvas, x, y)

    def _update(self, img: Image.Image, x: int, y: int):
        w, h = img.size
        bmi = BITMAPINFOHEADER(biSize=ctypes.sizeof(BITMAPINFOHEADER), biWidth=w, biHeight=-h,
                               biPlanes=1, biBitCount=32, biCompression=0)
        screen = user32.GetDC(None)
        mem = gdi32.CreateCompatibleDC(screen)
        bits = ctypes.c_void_p()
        hbmp = gdi32.CreateDIBSection(mem, ctypes.byref(bmi), 0, ctypes.byref(bits), None, 0)
        data = img.convert("RGBa").tobytes("raw", "BGRa")
        ctypes.memmove(bits, data, len(data))
        old = gdi32.SelectObject(mem, hbmp)
        blend = BLENDFUNCTION(0, 0, 255, AC_SRC_ALPHA)
        user32.UpdateLayeredWindow(self.hwnd, screen, ctypes.byref(wintypes.POINT(x, y)),
                                   ctypes.byref(wintypes.SIZE(w, h)), mem, ctypes.byref(wintypes.POINT(0, 0)),
                                   0, ctypes.byref(blend), ULW_ALPHA)
        gdi32.SelectObject(mem, old)
        gdi32.DeleteObject(hbmp)
        gdi32.DeleteDC(mem)
        user32.ReleaseDC(None, screen)


# ================================================================================================
# Demo driver
# ================================================================================================

class Demo:
    def __init__(self, speed: float, overlay: bool, out: Path):
        self.speed = speed
        self.overlay = Overlay(overlay)
        self.out = out
        self.cues: list[dict] = []
        self.t0 = 0.0
        self.sci = None
        self.top = None
        self.pid = None
        self._bubble_n = 0
        self._shown_key = None
        self._anchor = None

    # ---------------------------------------------------------------- setup
    def attach(self):
        print("Click into the EMPTY App Designer PeopleCode editor now…")
        while True:
            fg, focus = focused_window()
            if focus and class_name(focus).startswith("Scintilla"):
                break
            time.sleep(0.1)
        self.sci, self.top, self.pid = focus, user32.GetAncestor(focus, 2), window_pid(focus)
        if self.send(SCI_GETLENGTH) != 0:
            raise SystemExit("That editor isn't empty. Open an empty PeopleCode editor so nothing is overwritten.")
        print(f"Attached to Scintilla {self.sci:#x} in process {self.pid}. Starting in 2 seconds (hold F12 to stop).")
        self.overlay.place(self._window_rect(self.top))
        self.veil_up(0)
        time.sleep(2)
        self.t0 = time.perf_counter()

    def _client_rect(self, hwnd):
        """The text area in screen coordinates: no border, and no scrollbar or margin beside it."""
        r = wintypes.RECT()
        user32.GetClientRect(hwnd, ctypes.byref(r))
        p = wintypes.POINT(0, 0)
        user32.ClientToScreen(hwnd, ctypes.byref(p))
        return (p.x, p.y, p.x + r.right, p.y + r.bottom)

    def _window_rect(self, hwnd):
        r = wintypes.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        return (r.left, r.top, r.right, r.bottom)

    def send(self, msg, wp=0, lp=0) -> int:
        return user32.SendMessageW(self.sci, msg, wp, lp)

    def check(self):
        if user32.GetAsyncKeyState(VK_F12) & 0x8000:
            raise Aborted("F12 pressed")
        fg = user32.GetForegroundWindow()
        # AppRefiner's own dialogs (Rename) belong to its process but are owned by App Designer
        owner = user32.GetWindow(fg, GW_OWNER) if fg else None
        if not fg or (window_pid(fg) != self.pid and not (owner and window_pid(owner) == self.pid)):
            raise Aborted("App Designer is no longer the foreground window")

    def sleep(self, seconds: float):
        end = time.perf_counter() + seconds * self.speed
        while time.perf_counter() < end:
            if user32.GetAsyncKeyState(VK_F12) & 0x8000:
                raise Aborted("F12 pressed")
            time.sleep(0.02)

    # ---------------------------------------------------------------- document helpers
    def text(self) -> str:
        n = self.send(SCI_GETLENGTH)
        return bytes(self.send(SCI_GETCHARAT, i) & 0xFF for i in range(n)).decode("utf-8", "replace")

    def find(self, needle: str, nth: int = 1, after: str | None = None) -> int:
        t = self.text()
        if after and after not in t:
            raise ValueError(f"anchor '{after}' not found in the editor")
        start = t.index(after) + len(after) if after else 0
        pos = start - 1
        for _ in range(nth):
            pos = t.find(needle, pos + 1)
            if pos < 0:
                raise ValueError(f"'{needle}' not found in the editor")
        return pos

    def screen_point(self, pos: int):
        x, y = self.send(SCI_POINTXFROMPOSITION, 0, pos), self.send(SCI_POINTYFROMPOSITION, 0, pos)
        p = wintypes.POINT(x, y)
        user32.ClientToScreen(self.sci, ctypes.byref(p))
        return p.x, p.y

    def span_rect(self, pos: int, length: int):
        x0, y0 = self.screen_point(pos)
        x1, _ = self.screen_point(pos + length)
        lh = self.send(SCI_TEXTHEIGHT, self.send(SCI_LINEFROMPOSITION, pos))
        return (x0, y0, x1, y0 + lh)

    # ---------------------------------------------------------------- keyboard
    def _char(self, ch: str):
        code = ord(ch)
        _send(_key(scan=code, flags=KEYEVENTF_UNICODE), _key(scan=code, flags=KEYEVENTF_UNICODE | KEYEVENTF_KEYUP))

    def key(self, vk: int, ctrl=False, shift=False):
        self.check()
        mods = ([VK_CONTROL] if ctrl else []) + ([VK_SHIFT] if shift else [])
        seq = [_key(vk=m) for m in mods] + [_key(vk=vk), _key(vk=vk, flags=KEYEVENTF_KEYUP)]
        seq += [_key(vk=m, flags=KEYEVENTF_KEYUP) for m in reversed(mods)]
        _send(*seq)
        time.sleep(0.05)

    def _keystroke_delay(self, ch: str):
        base = random.uniform(0.03, 0.06)
        if ch in " ;,)(":
            base += random.uniform(0.01, 0.05)
        self.sleep(base)

    def type(self, text: str, pause_after: float = 0.4):
        """Types text like a person. Lines after a newline are indented exactly as written here,
        whatever the editor's own auto-indent does. Any open suggestion list is closed before a
        non-identifier character, so it never accepts something by accident.

        {rest} accepts from the suggestion list: "&gr{eeting}" types &gr, waits for the list
        and presses Tab, the way a person finishes a name. If no list shows up, it types "eeting"
        instead, so the code still comes out right."""
        i = 0
        while i < len(text):
            self.check()
            ch = text[i]
            if ch == "{":
                close = text.index("}", i)
                rest = text[i + 1:close]
                if self.wait_suggest(timeout=1.5, quiet=True):
                    self.sleep(0.45)
                    self.key(VK_TAB)
                    self.sleep(0.25)
                else:
                    print(f"  (no list to pick '{rest}' from; typing it)")
                    self.type(rest, pause_after=0)
                i = close + 1
                continue
            if not (ch.isalnum() or ch in "_&%#$") and self.send(SCI_AUTOCACTIVE):
                self.send(SCI_AUTOCCANCEL)
                time.sleep(0.03)
            if ch == "\n":
                # Input is queued, but our SendMessage calls jump that queue: wait until App
                # Designer has really taken the Enter before touching the new line
                lines = self.send(SCI_GETLINECOUNT)
                self.key(VK_RETURN)
                end = time.perf_counter() + 2
                while self.send(SCI_GETLINECOUNT) == lines and time.perf_counter() < end:
                    time.sleep(0.01)
                j = i + 1
                while j < len(text) and text[j] == " ":
                    j += 1
                line = self.send(SCI_LINEFROMPOSITION, self.send(SCI_GETCURRENTPOS))
                self.send(SCI_SETLINEINDENTATION, line, j - i - 1)
                self.send(SCI_GOTOPOS, self.send(SCI_GETLINEINDENTPOSITION, line))
                i = j
                self.sleep(random.uniform(0.08, 0.15))
                continue
            if ch in ')"':
                # Auto-pairing already put this closer here. It normally types over it, but loses
                # track when AppRefiner inserted a picked suggestion in between, so step over it.
                cur = self.send(SCI_GETCURRENTPOS)
                if cur < self.send(SCI_GETLENGTH) and self.send(SCI_GETCHARAT, cur) == ord(ch):
                    self.send(SCI_GOTOPOS, cur + 1)
                    self._keystroke_delay(ch)
                    i += 1
                    continue
            self._char(ch)
            self._keystroke_delay(ch)
            i += 1
        self.sleep(pause_after)

    def wait_suggest(self, timeout: float = 4.0, quiet: bool = False) -> bool:
        end = time.perf_counter() + timeout
        while time.perf_counter() < end:
            if self.send(SCI_AUTOCACTIVE):
                return True
            time.sleep(0.03)
        if not quiet:
            print("  (suggestion list didn't appear)")
        return False

    def pick(self, prefix: str, linger: float = 1.2, accept: int = VK_TAB, letter_gap: float = 0.0):
        """Types prefix into an open suggestion list, lets it sit, then accepts it (Tab by
        default, or VK_RETURN). letter_gap slows the typing so each narrowing step shows."""
        self.wait_suggest()
        for ch in prefix:
            self.check()
            self._char(ch)
            self._keystroke_delay(ch)
            self.track_hilite()
            self.sleep(letter_gap)
        self.sleep(linger)
        if self.send(SCI_AUTOCACTIVE):
            self.key(accept)
        self.sleep(0.3)
        self.track_hilite()

    def ctrl_space(self):
        self.key(VK_SPACE, ctrl=True)

    def escape(self):
        self.key(VK_ESCAPE)

    def goto_end_of(self, needle: str, nth: int = 1):
        """Clicks just after needle, the way a person would put the caret there."""
        pos = self.find(needle, nth) + len(needle.encode("utf-8"))
        self.glide_to(*self._mid_of_line(pos))
        _send(_mouse(flags=MOUSEEVENTF_LEFTDOWN), _mouse(flags=MOUSEEVENTF_LEFTUP))
        time.sleep(0.25)  # let the queued click land first
        self.send(SCI_GOTOPOS, pos)  # exact caret, whatever pixel the click landed on
        self.sleep(0.3)

    def click_on(self, needle: str, nth: int = 1, after: str | None = None):
        """Clicks inside needle, putting the caret one character into it."""
        pos = self.find(needle, nth, after)
        x0, y0, x1, y1 = self.span_rect(pos, len(needle.encode("utf-8")))
        self.glide_to((x0 + x1) // 2, (y0 + y1) // 2)
        _send(_mouse(flags=MOUSEEVENTF_LEFTDOWN), _mouse(flags=MOUSEEVENTF_LEFTUP))
        time.sleep(0.25)  # let the queued click land first
        self.send(SCI_GOTOPOS, pos + 1)
        self.sleep(0.3)

    def quick_fix(self, needle: str, nth: int = 1, after: str | None = None, linger: float = 1.6,
                  say: str | None = None):
        """Clicks on a squiggle, presses Ctrl+. and accepts the first fix offered. With say,
        a bubble explains the list while it's open."""
        self.click_on(needle, nth, after)
        self.mouse_away()
        self.key(VK_OEM_PERIOD, ctrl=True)
        if self.wait_suggest():
            if say:
                self.say(say, at=needle, nth=nth, after=after)
            self.sleep(linger)
            # The fix moves text; an outline left up would point at where things used to be
            self.unsay()
            self.key(VK_RETURN)
        self.unsay()
        self.sleep(0.8)
        # The click left the caret inside the squiggled word; scenes carry on typing at the end
        self.send(SCI_GOTOPOS, self.send(SCI_GETLENGTH))

    def rename(self, needle: str, new_name: str, nth: int = 1, after: str | None = None):
        """Renames the variable at needle with Ctrl+Shift+R: types new_name into AppRefiner's
        Rename dialog (the old name is pre-selected, so typing replaces it) and presses Enter."""
        self.click_on(needle, nth, after)
        self.mouse_away()
        self.unsay()
        self.key(ord("R"), ctrl=True, shift=True)
        end = time.perf_counter() + 4
        while window_pid(user32.GetForegroundWindow()) == self.pid and time.perf_counter() < end:
            time.sleep(0.05)
        self.sleep(0.8)
        for ch in new_name:
            self.check()
            self._char(ch)
            self._keystroke_delay(ch)
        self.sleep(0.8)
        self.key(VK_RETURN)
        end = time.perf_counter() + 4
        while window_pid(user32.GetForegroundWindow()) != self.pid and time.perf_counter() < end:
            time.sleep(0.05)
        self.sleep(0.6)
        self.send(SCI_GOTOPOS, self.send(SCI_GETLENGTH))

    def _mid_of_line(self, pos):
        x, y = self.screen_point(pos)
        lh = self.send(SCI_TEXTHEIGHT, self.send(SCI_LINEFROMPOSITION, pos))
        return x, y + lh // 2

    # ---------------------------------------------------------------- mouse
    def _move_abs(self, x, y):
        vx, vy = user32.GetSystemMetrics(76), user32.GetSystemMetrics(77)
        vw, vh = user32.GetSystemMetrics(78), user32.GetSystemMetrics(79)
        ax = int((x - vx) * 65535 / max(1, vw - 1))
        ay = int((y - vy) * 65535 / max(1, vh - 1))
        _send(_mouse(ax, ay, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK))

    def glide_to(self, x, y, duration: float = 0.45):
        self.check()
        p = wintypes.POINT()
        user32.GetCursorPos(ctypes.byref(p))
        sx, sy = p.x, p.y
        steps = max(8, int(duration * 60))
        for k in range(1, steps + 1):
            t = k / steps
            e = 1 - (1 - t) ** 3
            self._move_abs(round(sx + (x - sx) * e), round(sy + (y - sy) * e))
            time.sleep(duration / steps)

    def hover(self, needle: str, nth: int = 1, hold: float = 3.0, after: str | None = None):
        """Rests the mouse on needle until AppRefiner's hover appears, then holds it there."""
        pos = self.find(needle, nth, after)
        x0, y0, x1, y1 = self.span_rect(pos, len(needle.encode("utf-8")))
        self.glide_to((x0 + x1) // 2, (y0 + y1) // 2)
        end = time.perf_counter() + 5
        while time.perf_counter() < end and not self.send(SCI_CALLTIPACTIVE):
            time.sleep(0.05)
        self.sleep(hold)

    def mouse_away(self, cancel_tip: bool = True):
        """Parks the mouse in the empty part of the editor, which closes the hover. Moving off
        a squiggle before Ctrl+. also stops its hover from covering the quick-fix list."""
        r = wintypes.RECT()
        user32.GetWindowRect(self.sci, ctypes.byref(r))
        self.glide_to(r.right - 60, r.bottom - 60, 0.35)
        if cancel_tip:
            self.send(SCI_CALLTIPCANCEL)
        self.sleep(0.3)

    # ---------------------------------------------------------------- bubbles
    def say(self, text: str, at: str | None = None, nth: int = 1, title: str | None = None,
            side: str = "above", after: str | None = None, hold: float | None = None,
            every: bool = False, center: bool = False, scale: float = 1.0):
        """Shows a bubble pointing at the first (or nth) occurrence of `at`, or a caption along the
        bottom of the editor when `at` is None (or its middle, with center). With every, every occurrence of `at` is outlined
        (the bubble still points at the first). It replaces the previous bubble. With hold, waits
        that long (scaled by --speed) before returning; the bubble stays up until the next say()
        or unsay()."""
        self.unsay()
        img, tip = render_bubble(text, title, None if at is None else ("down" if side == "above" else "up"), scale)
        hilite = None
        hilites = None
        if at is not None and every:
            t, n, found = self.text(), len(at.encode("utf-8")), []
            i = t.find(at)
            while i >= 0:
                found.append(self.span_rect(i, n))
                i = t.find(at, i + 1)
            hilites = found
        self._anchor = None
        if at is None:
            r = wintypes.RECT()
            user32.GetWindowRect(self.sci, ctypes.byref(r))
            pos = ((r.left + r.right) // 2 - img.width // 2,
                   (r.top + r.bottom) // 2 - img.height // 2 if center else r.bottom - img.height - 40)
        else:
            p = self.find(at, nth, after)
            hilite = self.span_rect(p, len(at.encode("utf-8")))
            self._anchor = p
            cx = (hilite[0] + hilite[2]) // 2
            ty = hilite[1] - 4 if side == "above" else hilite[3] + 4
            pos = (cx - tip[0], ty - tip[1])
            # keep it inside App Designer horizontally (the pointer then leans, which is fine)
            wl, _, wr, _ = self._window_rect(self.top)
            pos = (min(max(pos[0], wl + 8), wr - img.width - 8), pos[1])
        self._bubble_n += 1
        key = f"b{self._bubble_n}"
        self.overlay.show(key, {"img": img, "pos": pos, "hilite": hilite, "hilites": hilites})
        self._shown_key = key
        png = self.out / f"cue_{self._bubble_n:02d}.png"
        img.save(png)
        self.cues.append({"n": self._bubble_n, "start": time.perf_counter() - self.t0, "end": None,
                          "text": (title + ": " if title else "") + text, "png": png.name,
                          "x": pos[0], "y": pos[1]})
        if hold:
            self.sleep(hold)

    def track_hilite(self):
        """Stretches the shown bubble's outline from its anchor to the caret, so it covers what's
        been typed since (the narrowing letters, then the accepted name). Same line only."""
        if not self._shown_key or self._anchor is None:
            return
        caret = self.send(SCI_GETCURRENTPOS)
        if caret <= self._anchor or (self.send(SCI_LINEFROMPOSITION, caret)
                                     != self.send(SCI_LINEFROMPOSITION, self._anchor)):
            return
        self.overlay.update(self._shown_key, {"hilite": self.span_rect(self._anchor, caret - self._anchor)})

    def drop_hilite(self):
        """Removes the shown bubble's outline but keeps the bubble."""
        if self._shown_key:
            self.overlay.update(self._shown_key, {"hilite": None, "hilites": None})
        self._anchor = None

    def unsay(self):
        if self._shown_key:
            self.overlay.hide(self._shown_key)
            self.cues[-1]["end"] = time.perf_counter() - self.t0
            self._shown_key = None

    def veil_up(self, seconds: float = 1.2):
        """Fades a white veil over the code pane, so a scene ends gently instead of stopping."""
        self.unsay()
        x0, y0, x1, y1 = self._client_rect(self.sci)
        # leave the line-number and folding margins showing
        margins = sum(self.send(SCI_GETMARGINWIDTHN, i) for i in range(self.send(SCI_GETMARGINS)))
        self.overlay.show("veil", {"veil": (x0 + margins, y0, x1 - VEIL_RIGHT_INSET, y1), "fade": seconds})
        if seconds:
            self.sleep(seconds + 0.1)

    def veil_down(self, seconds: float = 0.9):
        """Fades the veil back out to reveal the next scene."""
        self.overlay.hide("veil", seconds)
        self.sleep(seconds)

    def clear_editor(self):
        """Empties the editor between scenes, under the veil. Only ever called on text this
        script typed."""
        self.unsay()
        self.mouse_away()
        self.sleep(0.3)
        self.send(SCI_CLEARALL)
        self.sleep(0.6)

    def write_cues(self):
        self.unsay()
        with open(self.out / "cues.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=["n", "start", "end", "text", "png", "x", "y"])
            w.writeheader()
            for c in self.cues:
                w.writerow({**c, "start": f"{c['start']:.2f}", "end": f"{(c['end'] or c['start']):.2f}"})
        print(f"Cue sheet: {self.out / 'cues.csv'}  (times are seconds after the demo started)")


# ================================================================================================
# Scenes
# ================================================================================================

def scene_type_errors(d: Demo):
    d.say("AppRefiner type-checks PeopleCode as you type, before you ever save.",
          title="Type checking", hold=3.5)
    d.unsay()
    d.type('Local string &greeting = "Hello";\n')
    d.type("Local number &count = &gr{eeting};", pause_after=1.8)
    d.say("Assigning a string to a number is flagged straight away.", at="Local number &count = &greeting;")
    d.hover("&greeting", nth=2, hold=3.0)
    d.mouse_away()
    d.unsay()

    d.type("\nLocal number &rounded = Round(&gr{eeting}, 2);", pause_after=1.8)
    d.say("Builtin function arguments are checked against their signatures.", at="&greeting", nth=3)
    d.hover("&greeting", nth=3, hold=3.0)
    d.mouse_away()
    d.unsay()

    d.type("\nLocal Rowset &rs = CreateRowset(Record.PSOPRDEFN);\n&rs.", pause_after=0.2)
    d.say("Typing a dot lists the members of the object's type.", at="&rs.", nth=1, after="Rowset &rs =", hold=2.5)
    d.unsay()
    d.type('Flll("WHERE OPRID = :1", %OperatorI{d});', pause_after=1.8)
    d.say("A misspelt method name is caught too.", at="Flll")
    d.hover("Flll", hold=2.5)
    d.mouse_away()
    d.unsay()

    d.type("\nLocal array of string &names = CreateArrayRept(\"\", 0);\n&names.Pu{sh}(42);", pause_after=1.8)
    d.say("Arrays know their element type: a number can't go into an array of string.", at="42")
    d.hover("42", hold=3.0)
    d.mouse_away()
    d.unsay()
    d.sleep(0.8)


def scene_suggestions(d: Demo):
    d.say("AppRefiner suggests as you type: variables, system variables, members, functions "
          "and their parameters.", title="Suggestions", hold=4.0)
    d.unsay()

    d.type('Local string &greeting = "Hello";\nLocal string &who = %', pause_after=0.2)
    d.wait_suggest()
    d.say("% lists the system variables, with their types.", at="%", hold=3.0)
    d.say("Type a few letters to narrow it down, then Tab to accept.", at="%")
    d.pick("UserI", linger=1.4, letter_gap=0.25)
    d.type(";", pause_after=0.3)
    d.unsay()

    d.type("\nLocal string &message = &", pause_after=0.2)
    d.wait_suggest()
    d.say("& lists the variables in scope, with their types.", at="&", after="&message =", hold=3.0)
    d.say("Enter accepts too.", at="&", after="&message =")
    d.pick("gr", linger=1.4, accept=VK_RETURN, letter_gap=0.25)
    d.unsay()
    d.type(' | ", " | &wh{o};', pause_after=0.8)

    d.type("\nLocal number &next = GetNext", pause_after=0.3)
    d.say("Ctrl+Space on part of a name lists the matching builtin functions.", at="GetNext", hold=1.5)
    d.ctrl_space()
    d.pick("NumberW", linger=1.2)
    d.type("(", pause_after=0.4)
    d.unsay()
    d.say("Signature help shows the parameters, highlighting the one you're on.",
          at="GetNextNumberWithGaps", hold=0.8)
    d.drop_hilite()
    d.type("PSPRCSRQST.PRCSINSTANCE, ", pause_after=1.6)
    d.type("999999999, 1);", pause_after=1.5)
    d.unsay()
    d.sleep(0.8)


def scene_quick_fixes(d: Demo):
    d.say("Many squiggles come with a fix. Put the cursor on one and press Ctrl+.",
          title="Quick fixes", hold=3.5)
    d.unsay()

    d.type("Function Greet(&who As string) Returns string\n"
           '   Return Salutation() | ", " | &wh{o};\n'
           "End-Function;\n\n"
           "Function Salutation() Returns string\n"
           '   Return "Hello";\n'
           "End-Function;", pause_after=1.8)
    d.say("App Designer won't compile a call to a function defined further down.", at="Salutation")
    d.hover("Salutation", hold=2.5)
    d.unsay()
    d.quick_fix("Salutation", say="Ctrl+. lists the fixes for the squiggle under the cursor.")
    d.say("The function is moved above its caller.", at="Function Salutation", hold=3.0)
    d.unsay()

    d.type("\n\nLocal Rowset &rs = CreateRowset(Record.PSOPRDEFN);\n&rs.GetR{ow}(1);", pause_after=1.8)
    d.say("App Designer won't compile a return value that's thrown away.", at="GetRow")
    d.hover("GetRow", hold=2.5)
    d.unsay()
    d.quick_fix("GetRow", say="Assign result to a new local variable…")
    d.say("…keeps the result in a new local, already typed to match.", at="&_", hold=3.0)
    d.unsay()

    d.type('\n&rs.Fi{ll}("WHERE OPRID = :1", %OperatorI{d});', pause_after=1.2)
    d.say("Refactors too: Ctrl+Shift+R renames a variable everywhere it's used.",
          title="Rename", at="&rs", after="&_ =", hold=2.5)
    d.rename("&rs", "&operators", after="&_ =")
    d.say("Every use of &rs is now &operators.", at="&operators", every=True, hold=3.0)
    d.unsay()

    d.type("\n\nLocal string &unused;", pause_after=1.8)
    d.say("Unused variables are greyed out…", at="&unused", hold=2.2)
    d.unsay()
    d.quick_fix("&unused", say="…and one quick fix deletes the declaration.")
    d.sleep(1.5)
    d.sleep(0.8)


def scene_records_and_fields(d: Demo):
    d.say("Record and field code gets far fewer false errors, and real mistakes are still caught.",
          title="Records and fields", hold=4.0)
    d.unsay()

    d.type("Function LoadRole(&name As string) Returns Record\n"
           "   Local Record &role = CreateRecord(Record.PSROLEDEFN);\n"
           "   &role.ROLENAME.Value = &na{me};\n"
           "   &role.SelectByK{ey}();\n"
           "   Return &ro{le};\n"
           "End-Function;", pause_after=1.2)
    d.say("LoadRole is only declared to return a Record…", at="Returns Record", hold=2.2)
    d.unsay()

    d.type('\n\nLocal Record &admin = LoadRole("ADMIN");\n'
           "Local string &descr = &admin.DESCR.Value;", pause_after=1.6)
    d.say("…but its Return makes &admin a PSROLEDEFN, so DESCR is a known field. No false error.",
          at="&admin")
    d.hover("&admin", hold=3.5)
    d.mouse_away()
    d.unsay()

    d.type('\n&admin.OPRDEFNDESC.Value = "Oops";', pause_after=1.8)
    d.say("A field PSROLEDEFN doesn't have is still an error.", at="OPRDEFNDESC")
    d.hover("OPRDEFNDESC", hold=3.0)
    d.mouse_away()
    d.unsay()

    d.type("\n\nLocal Record &rec = CreateRecord(Record.PSOPRDEFN);\n"
           '&rec.OPRDEFNDESC.Value = "Demo user";\n'
           "&rec = CreateRecord(Record.PSROLEDEFN);\n"
           '&rec.DESCR.Value = "Demo role";', pause_after=1.6)
    d.say("Reassigned to a PSROLEDEFN, &rec is checked against that record, not the first one it held.",
          at="&rec.DESCR", after="Demo user")
    d.hover("&rec.DESCR", after="Demo user", hold=3.5)
    d.mouse_away()
    d.unsay()

    d.type("\n\nLocal number &next = GetNextNumberWithGaps(PSPRCSRQST.PRCSINSTANCE, 999999999, 1);",
           pause_after=1.6)
    d.say("A bare REC.FIELD is accepted where a function wants a field name, as the compiler accepts it.",
          at="PSPRCSRQST.PRCSINSTANCE", hold=3.5)
    d.unsay()
    d.type("\nLocal number &bad = GetNextNumberWithGaps(&rec.DESCR, 999999999, 1);", pause_after=1.8)
    d.say("A field's value isn't a field name, and that's still caught.", at="&rec.DESCR", after="&bad")
    d.hover("&rec.DESCR", after="&bad", hold=3.0)
    d.mouse_away()
    d.unsay()
    d.sleep(0.8)


def scene_refinement(d: Demo):
    d.say("PeopleCode makes you declare API objects as plain ApiObject. "
          "AppRefiner works out what they really are.", title="ApiObject refinement", hold=4.0)
    d.unsay()

    d.type("Function OpenProfile(&conn As ApiObject) Returns ApiObject\n"
           "   Return &conn.GetCompIntfc(CompIntfc.USER_PROFILE);\n"
           "End-Function;", pause_after=1.6)
    d.say("&conn is only declared as ApiObject…", at="&conn", hold=2.2)
    d.say("…but only a Session has GetCompIntfc(), so that's what AppRefiner types it as.",
          at="&conn", side="above")
    d.hover("&conn", hold=3.5)
    d.mouse_away()
    d.unsay()

    d.type("\n\nLocal ApiObject &profile = OpenProfile(%Sess{ion});", pause_after=1.6)
    d.say("OpenProfile's Return gives its callers a component interface, not just ApiObject.",
          at="&profile")
    d.hover("&profile", hold=3.5)
    d.mouse_away()
    d.unsay()

    d.type("\nLocal ApiObject &sess = %Sess{ion};\n&sess.", pause_after=0.3)
    d.say("Members are suggested for the refined type: Session.", at="&sess.", after="Local ApiObject &sess", hold=2.5)
    d.unsay()
    d.type("Disconect();", pause_after=1.8)
    d.say("So mistakes are caught too, and the message says where the type came from.", at="Disconect")
    d.hover("Disconect", hold=3.5)
    d.mouse_away()
    d.unsay()

    # When refinement can't decide: help it along with an annotation
    d.goto_end_of("End-Function;")
    d.type("\n\nFunction DescribeFolder(&folder As ApiObject) Returns string\n"
           "   Return &folder.Name;\n"
           "End-Function;", pause_after=1.6)
    d.say("Many API types have a Name, so this time AppRefiner can't tell which one &folder is.",
          at="&folder")
    d.hover("&folder", hold=3.0)
    d.mouse_away()
    d.say("When refinement can't decide, you can help it along with a /* as … */ annotation.",
          at="&folder As ApiObject", hold=2.5)
    d.unsay()
    d.goto_end_of("&folder As ApiObject")
    d.type(" /* as ", pause_after=0.2)
    d.say("The annotation suggests the API types that fit here.", at="/* as", side="above")
    d.pick("Fold")
    d.type(" */", pause_after=1.4)
    d.unsay()
    d.say("App Designer sees an ordinary comment. AppRefiner now treats &folder as a Folder.",
          at="&folder", nth=2)
    d.hover("&folder", nth=2, hold=3.5)
    d.mouse_away()
    d.unsay()
    d.sleep(0.8)


def scene_component_interfaces(d: Demo):
    d.say("With a database connection, a component interface is read from its definition "
          "and checked against it.", title="Component interfaces", hold=4.0)
    d.unsay()

    d.type("Local ApiObject &ci = %Session.GetCompIntfc(CompIntfc.USER_PROFILE);", pause_after=1.4)
    d.say("&ci is typed as the USER_PROFILE component interface.", at="&ci")
    d.hover("&ci", hold=3.0)
    d.mouse_away()
    d.unsay()

    d.type("\n&ci.UserID = %OperatorI{d};\nLocal string &descr = &ci.", pause_after=0.3)
    d.say("Suggestions list the CI's own keys, properties and collections.", at="&ci.", nth=2, hold=2.5)
    d.pick("UserDesc")
    d.type(";", pause_after=1.0)
    d.unsay()
    d.say("Hover a property for its type, the record field behind it, and whether it's read-only.",
          at="UserDescription")
    d.hover("UserDescription", hold=3.5)
    d.mouse_away()
    d.unsay()

    d.type("\nLocal date &locked = &ci.AccountLockDte;", pause_after=1.8)
    d.say("A property the CI doesn't have is an error.", at="AccountLockDte")
    d.hover("AccountLockDte", hold=3.0)
    d.mouse_away()
    d.unsay()

    d.type("\nLocal ApiObject &roles = &ci.Roles;\nLocal ApiObject &role = &roles.Item(1);"
           "\nLocal string &roleName = &role.", pause_after=0.3)
    d.say("Collections and their items know where they sit in the CI, however deep.",
          at="&role.", after="&roleName", hold=2.5)
    d.pick("RoleN")
    d.type(";", pause_after=1.0)
    d.unsay()
    d.hover("&role.", after="&roleName", hold=3.0)
    d.mouse_away()
    d.sleep(0.8)


def scene_definition_names(d: Demo):
    d.say("Type a definition keyword and a dot, and AppRefiner lists the names from your database.",
          title="Definition name suggestions", hold=4.0)
    d.unsay()

    d.type("Local Record &opr = CreateRecord(Record.P", pause_after=0.2)
    d.say("Record, Field, Page, Component, CompIntfc, HTML, SQL, Message, Node, Operation and more.",
          at="Record.")
    d.pick("SOPRDEFN", linger=1.5)
    d.type(");", pause_after=0.6)
    d.unsay()

    d.type("\nLocal string &html = GetHTMLText(HTML.P", pause_after=0.2)
    d.say("The list narrows as you type, so even very large databases stay quick.", at="HTML.")
    d.pick("T", linger=1.8)
    d.type(");", pause_after=0.6)
    d.unsay()

    d.type("\nLocal ApiObject /* as ", pause_after=0.2)
    d.say("Annotation suggestions lead straight into definition names too.", at="/* as")
    d.pick("CompI", linger=0.8)
    d.type("U", pause_after=0.1)
    d.pick("SER_PROFILE", linger=1.0)
    d.type(" */ &user;\n&user.", pause_after=0.3)
    d.unsay()
    d.say("…and &user gets USER_PROFILE's members.", at="&user.", hold=3.0)
    d.escape()
    d.unsay()
    d.sleep(0.8)


def scene_java_and_ctrl_space(d: Demo):
    d.say("Java objects' members come from Java classes AppRefiner can't see, so they're never "
          "reported as unknown.", title="Java objects", hold=3.5)
    d.unsay()
    d.type('Local JavaObject &sb = CreateJavaObject("java.lang.StringBuilder");\n'
           '&sb.append("Hello, ").append(%OperatorI{d});\n'
           "Local string &text = &sb.toString();", pause_after=1.8)
    d.say("No false errors on Java method calls.", at="append")
    d.sleep(2.5)
    d.unsay()

    d.type("\n\nLocal Rowset &rs = CreateRowset(Record.PSOPRDEFN);\n&rs.Get", pause_after=0.6)
    d.escape()
    d.say("Closed the list by accident? Ctrl+Space brings it back, filtered to what you typed.",
          at="&rs.Get", hold=2.0)
    d.ctrl_space()
    d.pick("R", linger=1.5)
    d.type("(1);", pause_after=1.0)
    d.unsay()
    d.sleep(0.8)


SCENES = {1: scene_type_errors, 2: scene_suggestions, 3: scene_quick_fixes, 4: scene_records_and_fields,
          5: scene_refinement, 6: scene_component_interfaces, 7: scene_definition_names,
          8: scene_java_and_ctrl_space}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--scenes", default=",".join(str(k) for k in SCENES), help="comma-separated, e.g. 2 or 1,2")
    ap.add_argument("--speed", type=float, default=1.0, help="pause multiplier: 0.5 is twice as fast")
    ap.add_argument("--no-overlay", action="store_true", help="record a clean take (cue sheet still written)")
    ap.add_argument("--out", default=str(Path(__file__).with_name("out")), help="where cues.csv and bubble PNGs go")
    ap.add_argument("--seed", type=int, default=7, help="typing-rhythm seed, so takes match")
    ap.add_argument("--no-intro", action="store_true", help="skip the opening title bubble")
    args = ap.parse_args()

    random.seed(args.seed)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    d = Demo(args.speed, not args.no_overlay, out)
    d.attach()
    try:
        if not args.no_intro:
            d.say("A quick tour of what's new: type checking as you type, suggestions, quick fixes, "
                  "records and fields, ApiObject refinement, component interfaces and definition names.",
                  title="What's new in AppRefiner 1.4", center=True, scale=1.3, hold=6.0)
            d.unsay()
            d.sleep(0.6)
        for i, n in enumerate(int(s) for s in args.scenes.split(",")):
            if i:
                d.clear_editor()
            d.veil_down()
            SCENES[n](d)
            d.veil_up()
        print("Done. Stop the recording now; the white veil lifts in 3 seconds.")
        time.sleep(3)
    except Aborted as e:
        print(f"Stopped: {e}")
    finally:
        d.overlay.clear()
        d.write_cues()
        time.sleep(0.4)


if __name__ == "__main__":
    main()

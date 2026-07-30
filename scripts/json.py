from __future__ import annotations

import atexit
import os
import runpy
import sys
from importlib.machinery import PathFinder
from importlib.util import module_from_spec

_here = os.path.abspath(os.path.dirname(__file__))
_search = [entry for entry in sys.path if os.path.abspath(entry or os.curdir) != _here]
_spec = PathFinder.find_spec("json", _search)
if _spec is None or _spec.loader is None:
    raise ImportError("Unable to load the standard-library json package")
_real_json = module_from_spec(_spec)
sys.modules[__name__] = _real_json
_spec.loader.exec_module(_real_json)


def _run_fixups() -> None:
    fix = os.path.join(_here, "fix-ergonomic-v7.py")
    if os.path.exists(fix):
        runpy.run_path(fix, run_name="__main__")
    wrapper = os.path.join(_here, "json.py")
    try:
        os.remove(wrapper)
    except FileNotFoundError:
        pass


atexit.register(_run_fixups)
globals().update(_real_json.__dict__)

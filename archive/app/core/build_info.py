"""
TempestOS Build Information
"""

from dataclasses import dataclass


@dataclass(frozen=True)
class BuildInfo:
    build: str
    revision: str
    codename: str
    status: str


CURRENT_BUILD = BuildInfo(
    build="0008.3",
    revision="Foundation Alpha Rev A",
    codename="Developer Framework",
    status="Development",
)
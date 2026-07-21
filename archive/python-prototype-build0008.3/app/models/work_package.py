"""
Work Package Model
"""

from dataclasses import dataclass
from typing import List


@dataclass
class WorkPackage:

    id: str
    title: str
    purpose: str

    status: str

    dependencies: List[str]

    acceptance_criteria: List[str]
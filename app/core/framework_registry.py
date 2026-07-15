"""
Framework Registry
"""

from typing import Dict

from app.core.framework import Framework


class FrameworkRegistry:

    def __init__(self):

        self._frameworks: Dict[str, Framework] = {}

    def register(self, framework: Framework):

        if framework.name in self._frameworks:
            raise ValueError(
                f"Framework '{framework.name}' already registered."
            )

        self._frameworks[framework.name] = framework

    def initialise_all(self):

        for framework in self._frameworks.values():
            framework.initialise()

    def all(self):

        return self._frameworks.values()
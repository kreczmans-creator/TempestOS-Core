"""
===============================================================================
TempestOS

Foundation Framework Base Class

Purpose:
    Defines the common interface implemented by all Frameworks.

Engineering Rationale:
    Every Framework shall expose a common lifecycle so the Platform
    can initialise, validate and monitor it consistently.
===============================================================================
"""

from abc import ABC, abstractmethod


class Framework(ABC):

    def __init__(self, name: str, version: str = "1.0"):

        self.name = name
        self.version = version
        self.initialised = False

    @abstractmethod
    def initialise(self):
        """Initialise the framework."""

    def health(self):

        return {
            "name": self.name,
            "version": self.version,
            "status": "Healthy" if self.initialised else "Not Initialised"
        }
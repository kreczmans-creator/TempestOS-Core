"""
Configuration Framework
"""

from app.core.framework import Framework
from app.core.config import Config


class ConfigurationFramework(Framework):

    def __init__(self):

        super().__init__("Configuration Framework")

        self.config = None

    def initialise(self):

        self.config = Config()

        self.initialised = True
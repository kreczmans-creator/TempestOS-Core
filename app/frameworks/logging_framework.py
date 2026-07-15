"""
Logging Framework
"""

from app.core.framework import Framework
from app.core.logger import initialise_logging


class LoggingFramework(Framework):

    def __init__(self):

        super().__init__("Logging Framework")

        self.logger = None

    def initialise(self):

        self.logger = initialise_logging()

        self.initialised = True
"""
===============================================================================
TempestOS

Platform

Purpose:
    Starts all Foundation Alpha Frameworks.

===============================================================================
"""

from app.core.config import Config
from app.core.logger import initialise_logging


class Platform:

    def __init__(self):

        self.config = Config()

        self.logger = initialise_logging(
            self.config.get("logging", "level")
        )

    def start(self):

        self.logger.info("Starting TempestOS Platform")

        print()
        print("=" * 60)
        print("TempestOS")
        print(self.config.get("application", "version"))
        print("=" * 60)

        print("✓ Configuration Framework")
        print("✓ Logging Framework")
        print()

        print("Foundation Alpha READY")

    def shutdown(self):

        self.logger.info("Platform shutdown")

        print()
        print("Goodbye.")
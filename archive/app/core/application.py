"""
TempestOS Application Bootstrap
"""

from app.core.platform import Platform


class Application:
    def __init__(self):
        self.platform = Platform()

    def run(self):
        self.platform.start()

        input("\nPress ENTER to close TempestOS...")

        self.platform.shutdown()
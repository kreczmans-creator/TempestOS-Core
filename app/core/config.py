from pathlib import Path
import yaml


class Config:
    """
    TempestOS Configuration Manager

    Responsible for reading application configuration
    from config/settings.yaml.
    """

    def __init__(self):

        # Locate the root TempestOS folder
        self.root = Path(__file__).resolve().parents[2]

        # Locate configuration file
        self.config_file = self.root / "config" / "settings.yaml"

        # Load configuration
        with open(self.config_file, "r", encoding="utf-8") as file:
            self.data = yaml.safe_load(file)

    def get(self, *keys):
        """
        Retrieve nested configuration values.

        Example:
            config.get("database", "filename")
        """

        value = self.data

        for key in keys:
            value = value[key]

        return value
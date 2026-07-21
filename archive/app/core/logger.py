"""
===============================================================================
TempestOS
Foundation Alpha Rev A

Module:
    Logger

Purpose:
    Provides a single logging service for the TempestOS platform.

Engineering Rationale:
    All platform messages shall pass through a common logging interface.
    This provides consistent formatting, simplifies troubleshooting,
    supports future audit logging, and prevents uncontrolled console output.

Owner:
    Core Platform Framework

Status:
    Build 0008.1
===============================================================================
"""

import logging
from pathlib import Path


def initialise_logging(level: str = "INFO") -> logging.Logger:
    """
    Initialise the TempestOS logging framework.

    Parameters
    ----------
    level
        Logging level (INFO, DEBUG, WARNING, ERROR)

    Returns
    -------
    logging.Logger
        Configured TempestOS logger.
    """

    log_directory = Path("logs")
    log_directory.mkdir(exist_ok=True)

    logger = logging.getLogger("TempestOS")

    logger.setLevel(getattr(logging, level.upper()))

    if not logger.handlers:

        formatter = logging.Formatter(
            "%(asctime)s | %(levelname)-8s | %(message)s"
        )

        console = logging.StreamHandler()
        console.setFormatter(formatter)

        logfile = logging.FileHandler(
            log_directory / "tempestos.log",
            encoding="utf-8"
        )
        logfile.setFormatter(formatter)

        logger.addHandler(console)
        logger.addHandler(logfile)

    logger.info("Logging framework initialised.")

    return logger
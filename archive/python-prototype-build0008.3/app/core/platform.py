"""
===============================================================================
TempestOS

Platform

Purpose:
    Starts and coordinates the TempestOS platform.
===============================================================================
"""

from app.core.framework_registry import FrameworkRegistry
from app.core.work_package_registry import WorkPackageRegistry
from app.models.work_package import WorkPackage

from app.frameworks.configuration_framework import ConfigurationFramework
from app.frameworks.logging_framework import LoggingFramework


class Platform:

    def __init__(self):

        self.frameworks = FrameworkRegistry()
        self.work_packages = WorkPackageRegistry()

    def start(self):

        # Register work packages
        self.work_packages.add(
            WorkPackage(
                id="WP-0008.3.001",
                title="Developer Framework",
                purpose="Create TempestOS development infrastructure.",
                status="In Progress",
                dependencies=[],
                acceptance_criteria=[
                    "Model created",
                    "Registry created",
                    "Platform registered",
                ],
            )
        )

        # Register frameworks
        self.frameworks.register(ConfigurationFramework())
        self.frameworks.register(LoggingFramework())

        # Initialise frameworks
        self.frameworks.initialise_all()

        # Startup banner
        print()
        print("=" * 60)
        print("TempestOS")
        print("Foundation Alpha Rev A")
        print("=" * 60)
        print()

        # Framework status
        print("Frameworks")
        for framework in self.frameworks.all():
            health = framework.health()
            print(f"✓ {health['name']} ({health['status']})")

        print()

        # Work packages
        print("Work Packages")
        for wp in self.work_packages.all():
            print(f"• {wp.id} - {wp.title} ({wp.status})")

        print()
        print("Platform READY")

    def shutdown(self):

        print()
        print("Platform shutdown complete.")
    
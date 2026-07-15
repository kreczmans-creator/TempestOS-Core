"""
Work Package Registry
"""

from app.models.work_package import WorkPackage


class WorkPackageRegistry:

    def __init__(self):

        self.packages = []

    def add(self, package: WorkPackage):

        self.packages.append(package)

    def all(self):

        return self.packages
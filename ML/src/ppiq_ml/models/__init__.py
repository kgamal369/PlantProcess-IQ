"""Learned model families.

Each family is a separate subpackage with its own optional dependency extra. A
family performs numerical computation only. It never connects to a database, never
reads a physical schema, and never decides which model a product should serve.
"""

# Horis - School Timetable & Scheduling Engine

A high-performance automated course scheduling desktop application built with C# (.NET) and WPF, utilizing Google OR-Tools (CP-SAT Solver) for mathematical optimization.

---

## Overview

Generating conflict-free school timetables is a computationally intensive, NP-Hard combinatorial problem. Horis models school scheduling as a Constraint Satisfaction and Optimization Problem (COP), leveraging the CP-SAT solver to evaluate trillions of potential allocations and deliver mathematically optimal schedules within seconds.

---

## Key Features

### Mathematical Optimization & Constraint Modeling
* **Hard Constraints:**
  * Strict unavailability windows for instructors and classrooms.
  * Zero-collision allocation across teachers, classes, and shared physical spaces.
  * Maximum daily teaching hour limits per instructor and student cohort.
  * Lock-in support: Fixed manual assignments are integrated directly into the decision space.
  * Seamless instructor schedules: Elimination of idle gap hours (windows) for designated strict personnel.
* **Soft Constraints:**
  * Multi-objective penalty scoring to minimize fragmented day distribution across teaching staff.
* **Sliding Window Consecutive Limits:**
  * Enforces maximum consecutive lecture blocks (e.g., preventing 3+ consecutive hours of the same subject) via linear sliding window formulations.

### User Interface & Operations
* **Interactive Drag-and-Drop:** Intuitive schedule adjustments with real-time constraint validation and collision interception.
* **Cell Locking:** Direct right-click contextual locking for administrative pre-assignments.
* **Official Export Engine:** Generates print-ready HTML/PDF individual schedules and master overview sheets formatted according to institutional reporting standards.

---

## Technical Stack

* **Language:** C# (.NET)
* **UI Framework:** WPF (XAML)
* **Optimization Engine:** Google OR-Tools (CP-SAT Solver)
* **Architecture:** Constraint Satisfaction Problem (CSP) / Domain Reduction Modeling

---

## Architecture & Development Notes

The project architecture, data domain, and mathematical constraint definitions were formulated to address practical bottlenecks in institutional scheduling. Modern AI-assisted development tools were integrated into the engineering workflow to accelerate boilerplate scaffolding, refactor UI-to-engine event bindings, and streamline iterative constraint testing.

---

> ### 🚀 Try it Live! (Demo)
> You can download the compiled `.exe` from the **[Releases](#)** tab and test the application instantly using the demo credentials below:
> 
> **Email:** `test@horis.com`
> **Password:** `demo123`
> *(Note: The demo database is pre-populated with sample teachers and classes for testing purposes.)*

---

## License & Intellectual Property

* **Engine & Solver Core (`/Engine`, `/Models`):** Licensed under the permissive **MIT License** for open algorithmic use and educational reference.
* **Full Application & UI:** Proprietary. Turnkey reproduction, white-labeling, and commercial distribution of the application are strictly prohibited. See [LICENSE](LICENSE) for full legal terms.

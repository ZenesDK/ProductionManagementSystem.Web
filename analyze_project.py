import os
import sys
from pathlib import Path

def analyze_project(project_root_path):
    """
    Analyzes the project structure and content, writing everything to a single text file.
    """
    project_path = Path(project_root_path)

    if not project_path.exists():
        print(f"Error: Path '{project_path}' does not exist.")
        sys.exit(1)

    output_file = project_path / "project_structure_and_content.txt"

    # Define file extensions and directories to include/exclude
    # Adjust these lists based on your project's needs
    include_extensions = {'.cs', '.cshtml', '.json', '.config', '.xml', '.md', '.sql', '.txt'}
    exclude_dirs = {
        '__pycache__', '.pytest_cache', '.git', 'bin', 'obj', '.vs', '.vscode',
        'node_modules', '__pycache__', '.idea', 'dist', 'build', 'target'
    }

    with open(output_file, 'w', encoding='utf-8') as outfile:
        outfile.write("=" * 80 + "\n")
        outfile.write(f"PROJECT ANALYSIS REPORT FOR: {project_path}\n")
        outfile.write("=" * 80 + "\n\n")

        # Walk through the project directory
        for root, dirs, files in os.walk(project_path):
            # Modify dirs in-place to exclude unwanted directories
            dirs[:] = [d for d in dirs if d not in exclude_dirs]

            # Sort for consistent output
            dirs.sort()
            files.sort()

            current_root_path = Path(root)
            relative_root = current_root_path.relative_to(project_path)

            # Write directory header
            if relative_root != Path("."):
                outfile.write(f"\n--- DIRECTORY: {relative_root} ---\n")
            else:
                outfile.write("\n--- ROOT DIRECTORY ---\n")

            # Process files in the current directory
            for file_name in files:
                file_path = current_root_path / file_name
                relative_file_path = file_path.relative_to(project_path)

                # Check if file extension is included
                if file_path.suffix.lower() in include_extensions:
                    outfile.write(f"\n>>> FILE: {relative_file_path} <<<\n")
                    outfile.write("-" * (len(str(relative_file_path)) + 8) + "\n")
                    try:
                        with open(file_path, 'r', encoding='utf-8') as f:
                            content = f.read()
                            outfile.write(content)
                            # Ensure a clear separation between file contents
                            outfile.write("\n" + "="*20 + f" END OF {relative_file_path} " + "="*20 + "\n")
                    except UnicodeDecodeError:
                        outfile.write(f"Could not decode file {relative_file_path} as UTF-8.\n")
                        outfile.write("="*20 + f" END OF {relative_file_path} " + "="*20 + "\n")
                    except Exception as e:
                        outfile.write(f"Error reading file {relative_file_path}: {e}\n")
                        outfile.write("="*20 + f" END OF {relative_file_path} " + "="*20 + "\n")
                else:
                    # Optionally log skipped files (comment out if not needed)
                    # outfile.write(f"  (Skipped: {file_name})\n")
                    pass

        outfile.write("\n" + "=" * 80 + "\n")
        outfile.write("ANALYSIS COMPLETE\n")
        outfile.write("=" * 80 + "\n")

    print(f"Project analysis complete. Report saved to: {output_file}")

if __name__ == "__main__":
    # Use the current working directory as the default project root
    # Or change this path to your specific project folder
    PROJECT_ROOT = "." # Change this if needed, e.g., "/path/to/your/ProductionManagementSystem.Web"

    analyze_project(PROJECT_ROOT)

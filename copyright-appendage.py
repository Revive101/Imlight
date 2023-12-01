import os

def add_copyright_header(file_path, header):
    with open(file_path, 'r') as file:
        content = file.read()

    if header not in content:
        with open(file_path, 'w') as file:
            file.write(header + '\n\n' + content)

def process_files(folder_path, header):
    for root, dirs, files in os.walk(folder_path):
        for file_name in files:
            if file_name.endswith('.cs'):
                file_path = os.path.join(root, file_name)
                add_copyright_header(file_path, header)

if __name__ == "__main__":
    # Set your copyright header
    copyright_header = """
/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */
"""

    # Set the folder path to start the search
    current_folder = os.getcwd()

    # Recursively process '.cs' files in the current folder and sub-folders
    process_files(current_folder, copyright_header)

    print("Copyright headers added to '.cs' files.")

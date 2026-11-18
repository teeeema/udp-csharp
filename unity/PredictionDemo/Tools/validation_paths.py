"""Keep previous validation evidence intact when repeating a suite."""
import argparse,pathlib
def result_directory(root,default):
 parser=argparse.ArgumentParser();parser.add_argument('--output',default=default);args=parser.parse_args()
 path=(pathlib.Path(root)/args.output).resolve();path.mkdir(parents=True,exist_ok=False);return path

"""Current results only; supersedes the historical 108-assertion report writer."""
import runpy,pathlib
runpy.run_path(str(pathlib.Path(__file__).with_name('write_final_reports.py')),run_name='__main__')

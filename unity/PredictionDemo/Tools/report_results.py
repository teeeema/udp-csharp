"""Regenerate current verified reports; historical measurements are not current inputs."""
import runpy,pathlib
runpy.run_path(str(pathlib.Path(__file__).with_name('write_final_reports.py')),run_name='__main__')

# trifecta-roversim

Unity rover simulation that trains a driving policy with reinforcement learning (ML-Agents).

## Requirements

- Unity 6000.6
- **Python 3.10.x** (3.10.1 to 3.10.12). Newer versions will not work. On Windows, use [3.10.11](https://www.python.org/downloads/release/python-31011/), the last 3.10 release with an installer.

ML-Agents and PyTorch are installed automatically into `venv/` the first time you run training. This may take a few minutes. You can watch the progress through the debug console.

## Training

Press Play, choose **Train**, then **Run Training**. Results and the trained `.onnx` model are saved to `results/` or the folder you select.

## Controls

| Key | Action |
|---|---|
| F2 | Toggle camera overlay |
| F3 | Toggle debug console |
| `.` / `,` | Speed up / slow down time |

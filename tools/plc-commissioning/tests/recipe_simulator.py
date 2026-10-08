import argparse
import asyncio
from pathlib import Path
from test_recipe import RecipeSimulator


async def main():
    parser=argparse.ArgumentParser();parser.add_argument('--root',type=Path,required=True);parser.add_argument('--port',type=int,required=True)
    args=parser.parse_args();sim=RecipeSimulator(args.root)
    sim.motion_offset=.45
    sim.command_duration=.35
    await sim.start(args.port)
    print('Explicit loopback recipe simulator ready',flush=True)
    try:await asyncio.Future()
    finally:await sim.close()


if __name__=='__main__':asyncio.run(main())

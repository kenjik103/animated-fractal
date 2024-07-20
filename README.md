# animated-fractal

Another tutorial from the goat [Jasper Flick](https://catlikecoding.com/unity/tutorials/basics/jobs/)

![Screen Recording 2024-07-19 at 9 03 20 PM](https://github.com/user-attachments/assets/9a78f0e6-d013-4dc9-a992-c2311bcb1f61)

A project able to render ~100,000 to <1,000,000 shapes in a spinning fractal pattern (depending on how many vertices+triangles the shape has). Notably, each piece of the fractal depends on positional data from its parent piece, meaning a compute shader can't really be used here. This means aggressive optimizations on the CPU side were necessary, namely Burst compiling and parallel processing/threading 👍. A shader was also used to do procedural drawing.

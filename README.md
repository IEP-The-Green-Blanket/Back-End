# Back-End

This will be the Back-End repository for our Green Blanket website project.

## Notes

> **Git Workflow Guidelines**
>
> 1. **Create a Branch** - Don't push to `main` directly! Create a branch from `dev` (recommended) or `main`.
>    - (make sure you have the latest version before you make your own branch `git pull`)
> 2. **Work & Develop** - Make your changes on your feature branch
> 3. **Commit & Push changes** - When you done developing for the day but not ready with everyting you can commit and push to your own branch
>    - Command: `git add .`
>    - Command: `git commit -m"<commit message>"`
>    - Command: `git push`
> 4. **Resolve Conflicts** - When done with everything commit your changes and pull `dev` back into your branch to resolve any Git conflicts immediately
>    - Command: `git pull origin dev`
> 5. **Merge to Dev** - Once conflicts are resolved, merge your branch into `dev` (but usualy there wont be any conflicts if we work correctly)`
>    - Command: `git push origin dev`
> 6. **Release** - At the end of the sprint, we are going to merge `dev` to `main` to deploy it to the server

## Docker Setup

### Running the Container

Open a terminal in `[path]/[to]/[your]/back-end` and run:

```bash
docker compose up --build
```

To stop the back-end:

```bash
docker compose down
```

### Useful Commands

| Command                                | Description                 |
| -------------------------------------- | --------------------------- |
| `docker ps`                            | Show all running containers |
| `docker container rm <container name>` | Remove a specific container |
| `docker images`                        | List all available images   |
| `docker image rm <image name>`         | Remove a specific image     |

**<span style="color:red">Don't mess with the deploy-pipeline.yml, docker-compose.yml and the Dockerfile!</span>**

# Back-End

This will be the Back-End repository for our Green Blanket website project.

## Notes

> **Git Workflow Guidelines**
>
> 1. **Create a Branch** - Don't push to `main` directly! Create a branch from `dev` (recommended) or `main`
>    - Example: `test_branch`
> 2. **Work & Develop** - Make your changes on your feature branch
> 3. **Resolve Conflicts** - When done pull `dev` back into your branch to resolve any Git conflicts immediately
>    - Example: `git pull origin dev`
> 4. **Merge to Dev** - Once conflicts are resolved, merge your branch into `dev` (but usualy there wont be any conflicts if we work correctly)
> 5. **Release** - At the end of the sprint, we are going to merge `dev` to `main` to deploy it to the server

Dont mess with the deploy-pipline.yml file and the Dockerfile!

How to run docker file:

Go to the location where you Dockerfile is located.

Than run the command 'docker build -t green-blanket-backend .' (this will build the docker file)

Than run the command 'docker run -p 5000:8080 --name running-backend-green-blanket green-blanket-backend' (docker will run now on localhost:5000 in the conainer running-backend-green-blanket)

Handy commands:

- docker ps (this shows all the containers that are running.)
- docker run
- docker run --rm -p {the rest of the command} (this will close the container if you close the terminal)

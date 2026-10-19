// Bundles the Fable output into one file the ASP.NET app can serve.
//
// Without this the browser would meet `import { html } from "lit"` and have nowhere to
// look: bare specifiers are a build-time convention, not something a static file server
// can answer.
//
// Paths are resolved from this file's folder, so the build works from the repository
// root as the README runs it.
import { fileURLToPath } from 'node:url'

const here = (path) => fileURLToPath(new URL(path, import.meta.url))

export default {
  root: here('./Client/build'),
  build: {
    outDir: here('./Server/wwwroot'),
    emptyOutDir: true,
    // Readable output, because this is a sample and someone will open it.
    minify: false,
    rollupOptions: {
      input: here('./Client/build/App.js'),
      output: { entryFileNames: 'app.js', format: 'es' },
    },
  },
}

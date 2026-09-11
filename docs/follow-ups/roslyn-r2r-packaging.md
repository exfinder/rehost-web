# Roslyn ReadyToRun images for package consumers

`eng/RoslynReadyToRun.targets` crossgens `Microsoft.CodeAnalysis{,.CSharp}` so
in-repo hosts (tests, samples) start page compilation without JITting the
compiler. The images live in `artifacts/roslyn-r2r/` and are substituted into
`RuntimeCopyLocalItems` for projects that import the targets file.

Package consumers never see them: the `Rehost.WebForms` package ships only
`build/Rehost.WebForms.targets`, so `apps/WebFormsApplication` (and any
real consumer) runs IL Roslyn and pays the JIT cost on first page compile.
Meanwhile the local-feed pack flow builds the src projects in Release, firing
the producer and crossgenning images that flow never consumes.

Open questions before shipping R2R to consumers: images are RID-specific, so
the package would need `runtimes/<rid>/` layouts (large) or a separate
tooling package per RID; whether first-compile latency matters enough for
server workloads that warm up once; and whether `crossgen2` at consumer build
time (a targets-driven local produce, like the in-repo producer) is a better
shape than shipping images. Also decide whether the local-feed pack should
skip the producer to avoid wasted crossgen.

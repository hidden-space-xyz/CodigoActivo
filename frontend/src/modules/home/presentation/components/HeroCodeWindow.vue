<script setup lang="ts"></script>

<template>
  <div class="code-window" aria-hidden="true">
    <div class="code-window__bar">
      <span class="code-window__dot" style="background: var(--ca-coral)" />
      <span class="code-window__dot" style="background: var(--ca-amber)" />
      <span class="code-window__dot" style="background: var(--ca-green)" />
      <span class="code-window__file">codigo_activo.py</span>
    </div>
    <div class="code-window__body">
      <div class="code-window__comment"># programación para tod@s</div>
      <div>
        <span class="t-purple">def</span> <span class="t-cyan">aprender</span>(<span class="t-amber"
          >idea</span
        >):
      </div>
      <div class="code-window__indent">
        <span class="t-purple">return</span> <span class="t-cyan">crear</span>(idea)
      </div>
      <div class="code-window__gap" />
      <div>
        <span class="t-purple">for</span> idea <span class="t-purple">in</span> [<span
          class="t-coral"
          >"juegos"</span
        >, <span class="t-coral">"webs"</span>, <span class="t-coral">"robots"</span>]:
      </div>
      <div class="code-window__indent">
        <span class="t-cyan">print</span>(aprender(idea))<span class="code-window__cursor" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.code-window {
  background: var(--ca-bg-elevated);
  border: 1px solid rgba(255, 255, 255, 0.09);
  border-radius: 16px;
  box-shadow: 0 30px 80px rgba(0, 0, 0, 0.45);
  overflow: hidden;
  animation: ca-float 6s ease-in-out infinite;
}

.code-window__bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 13px 16px;
  border-bottom: 1px solid var(--ca-border);
  background: var(--ca-input-bg);
}

.code-window__dot {
  width: 11px;
  height: 11px;
  border-radius: 50%;
}

.code-window__file {
  margin-left: 8px;
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint);
}

.code-window__body {
  padding: 22px;
  font-family: var(--ca-font-mono);
  font-size: 14px;
  line-height: 1.9;
}

.code-window__comment {
  color: var(--ca-text-faint);
}

.code-window__indent {
  padding-left: 24px;
}

.code-window__gap {
  height: 14px;
}

.code-window__cursor {
  display: inline-block;
  width: 9px;
  height: 17px;
  background: var(--ca-green);
  margin-left: 6px;
  vertical-align: -3px;
  animation: ca-blink 1.1s step-end infinite;
}

.t-purple {
  color: var(--ca-purple);
}
.t-cyan {
  color: var(--ca-cyan);
}
.t-amber {
  color: var(--ca-amber);
}
.t-coral {
  color: var(--ca-coral);
}
.t-faint {
  color: var(--ca-text-faint);
}
</style>

// PPIQ-704: promote the website honesty-lint to a BLOCKING stage (before deploy).
stage("Website honesty-lint") {
  steps {
    dir("Website/PlantProcess.Website") {
      sh "node scripts/validate-phase7-content.mjs"
      sh "node scripts/check-tagline.mjs"
    }
  }
}
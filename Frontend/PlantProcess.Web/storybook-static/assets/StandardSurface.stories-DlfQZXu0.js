import{a as e,n as t}from"./chunk-DnJy8xQt.js";import{o as n,t as r}from"./iframe-Bm-vu0Mc.js";import{t as i}from"./jsx-runtime-DxP0NviS.js";import{n as a,t as o}from"./StandardButton-B2Y-eCAT.js";import{a as s,i as c,n as l,r as u,t as d}from"./StandardSurface-C7oWTi6o.js";function f(){let e=s();return(0,m.jsxs)(`div`,{style:{display:`flex`,gap:10,flexWrap:`wrap`},children:[(0,m.jsx)(o,{onClick:()=>e.notify({variant:`info`,title:`Info`,description:`Investigation started.`}),children:`Info`}),(0,m.jsx)(o,{variant:`success`,onClick:()=>e.notify({variant:`success`,title:`Saved`,description:`Configuration saved.`}),children:`Success`}),(0,m.jsx)(o,{variant:`secondary`,onClick:()=>e.notify({variant:`warning`,title:`Warning`,description:`Some rows need review.`}),children:`Warning`}),(0,m.jsx)(o,{variant:`danger`,onClick:()=>e.notify({variant:`error`,title:`Error`,description:`Refresh failed.`}),children:`Error`}),(0,m.jsx)(o,{variant:`ghost`,onClick:()=>e.notify({variant:`loading`,title:`Loading`,description:`Operation in progress.`}),children:`Loading`})]})}var p,m,h,g,_,v,y;t((()=>{p=e(n(),1),a(),c(),r(),m=i(),h={title:`PlantProcess IQ/Standard/Surface`,parameters:{layout:`fullscreen`}},g={render:()=>(0,m.jsx)(`div`,{className:`ppiq-std-standards-page`,children:(0,m.jsxs)(`div`,{className:`ppiq-std-standards-grid ppiq-std-standards-grid--two`,children:[(0,m.jsx)(d,{elevation:`flat`,title:`Flat card`,children:`Flat surface.`}),(0,m.jsx)(d,{elevation:`raised`,title:`Raised card`,children:`Raised surface.`}),(0,m.jsx)(d,{elevation:`floating`,title:`Floating card`,children:`Floating surface.`})]})})},_={render:()=>{let[e,t]=(0,p.useState)(!1),[n,r]=(0,p.useState)(!1);return(0,m.jsxs)(`div`,{className:`ppiq-std-standards-page`,children:[(0,m.jsxs)(`div`,{style:{display:`flex`,gap:12},children:[(0,m.jsx)(o,{onClick:()=>t(!0),children:`Open modal`}),(0,m.jsx)(o,{variant:`secondary`,onClick:()=>r(!0),children:`Open dirty modal`})]}),(0,m.jsx)(l,{open:e,title:`Standard modal`,description:`Focus-trapped dialog.`,onClose:()=>t(!1),footer:(0,m.jsx)(o,{onClick:()=>t(!1),children:`Confirm`}),children:`This modal closes on Escape and click outside.`}),(0,m.jsx)(l,{open:n,isDirty:!0,title:`Dirty modal`,description:`Click-outside is disabled when isDirty=true.`,onClose:()=>r(!1),footer:(0,m.jsx)(o,{onClick:()=>r(!1),children:`Save`}),children:`Click outside is blocked to prevent data loss.`})]})}},v={render:()=>(0,m.jsx)(u,{children:(0,m.jsx)(`div`,{className:`ppiq-std-standards-page`,children:(0,m.jsx)(f,{})})})},g.parameters={...g.parameters,docs:{...g.parameters?.docs,source:{originalSource:`{
  render: () => <div className="ppiq-std-standards-page">\r
      <div className="ppiq-std-standards-grid ppiq-std-standards-grid--two">\r
        <StandardCard elevation="flat" title="Flat card">Flat surface.</StandardCard>\r
        <StandardCard elevation="raised" title="Raised card">Raised surface.</StandardCard>\r
        <StandardCard elevation="floating" title="Floating card">Floating surface.</StandardCard>\r
      </div>\r
    </div>
}`,...g.parameters?.docs?.source}}},_.parameters={..._.parameters,docs:{..._.parameters?.docs,source:{originalSource:`{
  render: () => {
    const [open, setOpen] = useState(false);
    const [dirtyOpen, setDirtyOpen] = useState(false);
    return <div className="ppiq-std-standards-page">\r
        <div style={{
        display: "flex",
        gap: 12
      }}>\r
          <StandardButton onClick={() => setOpen(true)}>Open modal</StandardButton>\r
          <StandardButton variant="secondary" onClick={() => setDirtyOpen(true)}>Open dirty modal</StandardButton>\r
        </div>\r
\r
        <StandardModal open={open} title="Standard modal" description="Focus-trapped dialog." onClose={() => setOpen(false)} footer={<StandardButton onClick={() => setOpen(false)}>Confirm</StandardButton>}>\r
          This modal closes on Escape and click outside.\r
        </StandardModal>\r
\r
        <StandardModal open={dirtyOpen} isDirty title="Dirty modal" description="Click-outside is disabled when isDirty=true." onClose={() => setDirtyOpen(false)} footer={<StandardButton onClick={() => setDirtyOpen(false)}>Save</StandardButton>}>\r
          Click outside is blocked to prevent data loss.\r
        </StandardModal>\r
      </div>;
  }
}`,..._.parameters?.docs?.source}}},v.parameters={...v.parameters,docs:{...v.parameters?.docs,source:{originalSource:`{
  render: () => <StandardToastProvider>\r
      <div className="ppiq-std-standards-page">\r
        <ToastDemo />\r
      </div>\r
    </StandardToastProvider>
}`,...v.parameters?.docs?.source}}},y=[`Cards`,`Modal`,`Toasts`]}))();export{g as Cards,_ as Modal,v as Toasts,y as __namedExportsOrder,h as default};
import Icon from '../components/Icon'

const descriptions = {
  Users: 'User and role management will be available in the next stage.',
  Projects: 'Projects and project memberships will be available in the next stage.',
  Tasks: 'Project tasks and assigned tasks will be available in the next stage.',
  Profile: 'Personal details and profile picture updates will be available in the next stage.',
}

export default function Placeholder({ title }) {
  return <>
    <div className="page-heading"><h1>{title}</h1></div>
    <section className="placeholder"><span className="placeholder-icon"><Icon name={title.toLowerCase()} size={26} /></span><h2>This section is coming next</h2><p className="muted">{descriptions[title]}</p></section>
  </>
}

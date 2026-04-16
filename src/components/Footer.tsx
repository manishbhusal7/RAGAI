import React from 'react';
import './Footer.css';

const Footer: React.FC = () => {
  const currentYear = new Date().getFullYear();
  
  return (
    <footer className="app-footer">
      <div className="footer-container">
        <span className="footer-text">
          &copy; {currentYear} Personal Knowledge Assistant.
        </span>
      </div>
    </footer>
  );
};

export default Footer; 
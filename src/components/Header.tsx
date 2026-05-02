import React from 'react';
import { AppBar, Toolbar, Typography, IconButton, Box } from '@mui/material';
import { Menu as MenuIcon } from '@mui/icons-material';
import './Header.css';

interface HeaderProps {
  onToggleSidebar?: () => void;
  isSidebarCollapsed?: boolean;
}

const HEADER_TITLE = 'Personal Knowledge Assistant';
const HEADER_SUBTITLE = 'Conversations, documents, and workspace context in one place.';

const getSidebarToggleLabel = (isSidebarCollapsed?: boolean) => {
  return isSidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar';
};

const HeaderBrand: React.FC = () => {
  return (
    <div className="header-brand">
      <div className="header-brand-copy">
        <Typography
          variant="h6"
          component="h1"
          className="header-title"
        >
          {HEADER_TITLE}
        </Typography>
        <Typography
          variant="body2"
          component="p"
          className="header-subtitle"
        >
          {HEADER_SUBTITLE}
        </Typography>
      </div>
    </div>
  );
};

interface HeaderSidebarToggleProps {
  onToggleSidebar: () => void;
  isSidebarCollapsed?: boolean;
}

const HeaderSidebarToggle: React.FC<HeaderSidebarToggleProps> = ({
  onToggleSidebar,
  isSidebarCollapsed
}) => {
  return (
    <IconButton
      edge="start"
      onClick={onToggleSidebar}
      title={getSidebarToggleLabel(isSidebarCollapsed)}
      className="header-toggle-button"
    >
      <MenuIcon />
    </IconButton>
  );
};

const HeaderToolbarLayout: React.FC<HeaderProps> = ({ onToggleSidebar, isSidebarCollapsed }) => {
  return (
    <Toolbar className="header-toolbar">
      {onToggleSidebar && (
        <HeaderSidebarToggle
          onToggleSidebar={onToggleSidebar}
          isSidebarCollapsed={isSidebarCollapsed}
        />
      )}

      <HeaderBrand />

      <div className="header-spacer" />
    </Toolbar>
  );
};

const Header: React.FC<HeaderProps> = ({ onToggleSidebar, isSidebarCollapsed }) => {
  return (
    <AppBar 
      position="sticky" 
      className="app-header"
      elevation={0}
    >
      <HeaderToolbarLayout
        onToggleSidebar={onToggleSidebar}
        isSidebarCollapsed={isSidebarCollapsed}
      />
    </AppBar>
  );
};

export default Header; 
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
    <Box className="header-brand" sx={{ flex: 1, textAlign: 'center' }}>
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
    </Box>
  );
};

const Header: React.FC<HeaderProps> = ({ onToggleSidebar, isSidebarCollapsed }) => {
  return (
    <AppBar 
      position="sticky" 
      className="app-header"
      elevation={0}
      sx={{
        backgroundColor: '#ffffff',
        borderBottom: '1px solid #e5e7eb',
        color: '#1f2937'
      }}
    >
      <Toolbar sx={{ maxWidth: 1400, margin: '0 auto', width: '100%', justifyContent: 'space-between' }}>
        {onToggleSidebar && (
          <IconButton
            edge="start"
            onClick={onToggleSidebar}
            title={getSidebarToggleLabel(isSidebarCollapsed)}
            sx={{
              color: '#6b7280',
              '&:hover': {
                backgroundColor: '#f3f4f6',
                color: '#374151'
              }
            }}
          >
            <MenuIcon />
          </IconButton>
        )}
        
        <HeaderBrand />
        
        <Box sx={{ width: 48 }}> {/* Spacer to center the title */}
        </Box>
      </Toolbar>
    </AppBar>
  );
};

export default Header; 